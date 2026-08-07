using Carter;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using StackExchange.Redis;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Village.Api.Extensions;
using Village.Api.Hubs;
using Village.Api.Modules;
using Village.Api.Services;
using Village.Api;
using Village.Infrastructure.Data;

// Npgsql: tolerate DateTime with Kind=Unspecified (sent by web clients without Z suffix)
// Without this, query-string DateTimes with no timezone cause 500 errors on timestamptz columns.
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// Database
builder.Services.AddDbContext<VillageDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("Default"),
        npgsql => npgsql.MigrationsAssembly(typeof(VillageDbContext).Assembly.FullName)
    )
    );

// Auth
builder.Services.AddVillageAuth(builder.Configuration);
builder.Services.AddSingleton<IJwtService, JwtService>();

// Notifications
builder.Services.AddScoped<NotificationService>();

// Email
builder.Services.AddHttpClient<IEmailService, MailgunEmailService>();

// Stripe
Stripe.StripeConfiguration.ApiKey = Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY")
    ?? builder.Configuration["Stripe:SecretKey"];

// Recipe ideas — TheMealDB proxy (free, no API key)
builder.Services.AddHttpClient<MealDbService>();

// Carter modules
builder.Services.AddCarter();

// FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddFluentValidationAutoValidation();

// SignalR
builder.Services.AddSignalR()
    .AddStackExchangeRedis(builder.Configuration.GetConnectionString("Redis") ?? "redis:6379",
        options => { options.Configuration.ChannelPrefix = RedisChannel.Literal("Village"); });

// JSON: accept string enum values from frontend (e.g. "Once" not 0)
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});

// OpenAPI
builder.Services.AddOpenApi();

// CORS (dev — specific origin needed for SignalR credentials)
// Production CORS reads from env var CORS_ALLOWED_ORIGINS (comma-separated)
var prodOrigins = (Environment.GetEnvironmentVariable("CORS_ALLOWED_ORIGINS") ?? "")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(options =>
{
    options.AddPolicy("Dev", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:3000", "http://localhost:8080")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
    if (prodOrigins.Length > 0)
    {
        options.AddPolicy("Prod", policy =>
        {
            policy.WithOrigins(prodOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        });
    }
});

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionLogger>();

// Rate limiting
// - "Auth" policy (10 req/min): applied explicitly to login/register/refresh endpoints
// - GlobalLimiter (200 req/min per user/IP): applies to all other endpoints
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddFixedWindowLimiter("Auth", config =>
    {
        config.PermitLimit = 10;
        config.Window = TimeSpan.FromMinutes(1);
        config.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        config.QueueLimit = 2;
    });

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.User.Identity?.IsAuthenticated == true
                ? (httpContext.User.GetUserId()?.ToString()
                   ?? httpContext.Connection.RemoteIpAddress?.ToString()
                   ?? "anon")
                : (httpContext.Connection.RemoteIpAddress?.ToString() ?? "anon"),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 200,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 10
            }));
});

// Register outermost pipeline wrapper to catch exceptions BEFORE DeveloperExceptionPage
builder.Services.AddSingleton<IStartupFilter, ExceptionLoggingStartupFilter>();

// Forwarded headers for correct IP/protocol detection behind reverse proxy
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();

app.UseExceptionHandler(); // calls registered IExceptionHandler services
app.UseStatusCodePages();

app.UseMiddleware<Village.Api.Extensions.SecurityHeadersMiddleware>();

// FluentValidation: return 400 for validation errors
app.Use(async (context, next) =>
{
    try
    {
        await next(context);
    }
    catch (ValidationException ex)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        context.Response.ContentType = "application/json";
        var errors = ex.Errors.Select(e => new { field = e.PropertyName, message = e.ErrorMessage });
        await System.Text.Json.JsonSerializer.SerializeAsync(context.Response.Body, new { errors });
    }
});

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.WithTitle("Village API")
               .WithTheme(ScalarTheme.Purple)
               .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
    });
    app.UseCors("Dev");
}
else
{
    if (prodOrigins.Length > 0)
        app.UseCors("Prod");
}

app.UseAuthentication();
app.UseAuthorization();

app.UseRateLimiter();

app.MapCarter();

// SignalR hubs
app.MapHub<Village.Api.Hubs.FamilyHub>("/hubs/family");
app.MapHub<Village.Api.Hubs.ChoreHub>("/hubs/chores");
app.MapHub<Village.Api.Hubs.PointsHub>("/hubs/points");
app.MapHub<Village.Api.Hubs.NotificationsHub>("/hubs/notifications");
app.MapHub<Village.Api.Hubs.ShoppingHub>("/hubs/shopping");

// Health check
app.MapGet("/health", async (Village.Infrastructure.Data.VillageDbContext db, HttpContext http) =>
{
    var status = "healthy";
    var dbStatus = "unknown";
    var redisStatus = "unknown";

    try { await db.Database.CanConnectAsync(); dbStatus = "connected"; }
    catch { dbStatus = "unavailable"; status = "degraded"; }

    var redis = http.RequestServices.GetService<StackExchange.Redis.IConnectionMultiplexer>();
    if (redis != null)
    {
        try { redisStatus = redis.IsConnected ? "connected" : "disconnected"; }
        catch { redisStatus = "unavailable"; status = "degraded"; }
    }
    else
    {
        redisStatus = "not_configured";
    }

    return Results.Ok(new
    {
        status,
        service = "village-api",
        version = "0.2.0",
        timestamp = DateTime.UtcNow,
        database = dbStatus,
        redis = redisStatus
    });
}).AllowAnonymous();

// Auto-migrate in development only — production migrations are a separate deployment step
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<VillageDbContext>();
    await db.Database.MigrateAsync();
}

await app.RunAsync();
