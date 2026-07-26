using System.Text.Json;
using System.Threading.RateLimiting;
using Carter;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Village.Api.Extensions;
using Village.Api.Hubs;
using Village.Api.Modules;
using Village.Api.Services;
using Village.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

// Database
builder.Services.AddDbContext<VillageDbContext>(options =>
    options.UseSqlite(
        builder.Configuration.GetConnectionString("Default")
    ));

// Auth
builder.Services.AddVillageAuth(builder.Configuration);
builder.Services.AddSingleton<IJwtService, JwtService>();

// Notifications
builder.Services.AddScoped<NotificationService>();

// Carter modules
builder.Services.AddCarter();

// SignalR (in-memory backplane for dev)
builder.Services.AddSignalR();

// OpenAPI
builder.Services.AddOpenApi();

// ── Production hardening ──

// HTTP logging for diagnostics
builder.Services.AddHttpLogging(options =>
{
    options.LoggingFields = HttpLoggingFields.RequestMethod
                          | HttpLoggingFields.RequestPath
                          | HttpLoggingFields.ResponseStatusCode;
});

// Rate limiting — protect auth and invite endpoints from brute force
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("Auth", opt =>
    {
        opt.PermitLimit = 10;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 3;
    });

    options.AddFixedWindowLimiter("InviteLookup", opt =>
    {
        opt.PermitLimit = 20;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// CORS (dev — specific origin needed for SignalR credentials)
// Prod policy reads allowed origins from config — set Cors:AllowedOrigins in production appsettings
builder.Services.AddCors(options =>
{
    options.AddPolicy("Dev", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:3000", "http://localhost:8080")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });

    options.AddPolicy("Prod", policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
        if (origins is { Length: > 0 })
            policy.WithOrigins(origins);
        else
            policy.WithOrigins("https://village.app"); // fallback default
        policy.AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Middleware pipeline

// 1. Exception handler (outermost — catches everything)
app.UseExceptionHandler(exceptionHandlerApp =>
{
    exceptionHandlerApp.Run(async context =>
    {
        var feature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerPathFeature>();
        var exception = feature?.Error;

        int statusCode;
        string title;
        string detail;

        switch (exception)
        {
            case BadHttpRequestException:
            case System.Text.Json.JsonException:
                statusCode = StatusCodes.Status400BadRequest;
                title = "Bad Request";
                detail = exception.Message;
                break;
            case NotImplementedException:
                statusCode = StatusCodes.Status501NotImplemented;
                title = "Not Implemented";
                detail = "The requested feature is not implemented.";
                break;
            default:
                statusCode = StatusCodes.Status500InternalServerError;
                title = "Internal Server Error";
                detail = app.Environment.IsDevelopment()
                    ? exception?.Message ?? "An unexpected error occurred."
                    : "An unexpected error occurred.";
                break;
        }

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(new
        {
            status = statusCode,
            title,
            detail
        });
    });
});

// 2. Serve Flutter web static files (production — Flutter build copied to wwwroot/)
if (!app.Environment.IsDevelopment())
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

// 3. HTTP request logging
app.UseHttpLogging();

// 4. Environment-specific middleware
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

// Rate limiter middleware (after auth, before endpoints)
app.UseRateLimiter();

app.MapCarter();

// SignalR hubs
app.MapHub<Village.Api.Hubs.FamilyHub>("/hubs/family");
app.MapHub<Village.Api.Hubs.ChoreHub>("/hubs/chores");
app.MapHub<Village.Api.Hubs.PointsHub>("/hubs/points");
app.MapHub<Village.Api.Hubs.NotificationsHub>("/hubs/notifications");
app.MapHub<Village.Api.Hubs.SchoolHub>("/hubs/school");
app.MapHub<Village.Api.Hubs.MealPlanHub>("/hubs/mealplan");

// Health check
app.MapGet("/", () => Results.Redirect("/scalar/v1")).AllowAnonymous().ExcludeFromDescription();

app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    service = "village-api",
    version = "0.1.0",
    timestamp = DateTime.UtcNow
})).AllowAnonymous();

// Apply migrations + seed on startup (dev only)
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<VillageDbContext>();
    await db.Database.MigrateAsync();
    await Village.Infrastructure.Data.DbInitializer.SeedAsync(db);
}

await app.RunAsync();
