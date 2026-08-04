using Carter;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using StackExchange.Redis;
using Microsoft.AspNetCore.Diagnostics;
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

// Rate limiting — protect auth endpoints from brute force
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("Auth", config =>
    {
        config.PermitLimit = 10;
        config.Window = TimeSpan.FromMinutes(1);
        config.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        config.QueueLimit = 2;
    });
});

// Register outermost pipeline wrapper to catch exceptions BEFORE DeveloperExceptionPage
builder.Services.AddSingleton<IStartupFilter, ExceptionLoggingStartupFilter>();

var app = builder.Build();

app.UseExceptionHandler(); // calls registered IExceptionHandler services
app.UseStatusCodePages();

// app.UseMiddleware<Village.Api.Extensions.SecurityHeadersMiddleware>(); // TEMP disabled for debugging

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

// Health check
app.MapGet("/health", async (Village.Infrastructure.Data.VillageDbContext db, StackExchange.Redis.IConnectionMultiplexer? redis) =>
{
    var status = "healthy";
    var dbStatus = "unknown";
    var redisStatus = "unknown";

    try { await db.Database.CanConnectAsync(); dbStatus = "connected"; }
    catch { dbStatus = "unavailable"; status = "degraded"; }

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

// Apply pending migrations on startup
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<VillageDbContext>();
    await db.Database.MigrateAsync();
    await Village.Infrastructure.Data.DbInitializer.SeedAsync(db);
}
else
{
    // Apply migrations in production too
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<VillageDbContext>();
    await db.Database.MigrateAsync();
}

await app.RunAsync();
