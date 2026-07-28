using Carter;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using StackExchange.Redis;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Village.Api.Extensions;
using Village.Api.Hubs;
using Village.Api.Modules;
using Village.Api.Services;
using Village.Api;
using Village.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

// Database
builder.Services.AddDbContext<VillageDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("Default"),
        npgsql => npgsql.MigrationsAssembly(typeof(VillageDbContext).Assembly.FullName)
    )
    .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)));

// Auth
builder.Services.AddVillageAuth(builder.Configuration);
builder.Services.AddSingleton<IJwtService, JwtService>();

// Notifications
builder.Services.AddScoped<NotificationService>();

// Carter modules
builder.Services.AddCarter();

// SignalR
builder.Services.AddSignalR()
    .AddStackExchangeRedis(builder.Configuration.GetConnectionString("Redis") ?? "redis:6379",
        options => { options.Configuration.ChannelPrefix = RedisChannel.Literal("Village"); });

// OpenAPI
builder.Services.AddOpenApi();

// CORS (dev — specific origin needed for SignalR credentials)
builder.Services.AddCors(options =>
{
    options.AddPolicy("Dev", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:3000", "http://localhost:8080")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionLogger>();

// Register outermost pipeline wrapper to catch exceptions BEFORE DeveloperExceptionPage
builder.Services.AddSingleton<IStartupFilter, ExceptionLoggingStartupFilter>();

var app = builder.Build();

app.UseExceptionHandler(); // calls registered IExceptionHandler services
app.UseStatusCodePages();

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

app.UseAuthentication();
app.UseAuthorization();

app.MapCarter();

// SignalR hubs
app.MapHub<Village.Api.Hubs.FamilyHub>("/hubs/family");
app.MapHub<Village.Api.Hubs.ChoreHub>("/hubs/chores");
app.MapHub<Village.Api.Hubs.PointsHub>("/hubs/points");
app.MapHub<Village.Api.Hubs.NotificationsHub>("/hubs/notifications");

// Health check
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
