using Carter;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Village.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

// Database
builder.Services.AddDbContext<VillageDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("Default"),
        npgsql => npgsql.MigrationsAssembly(typeof(VillageDbContext).Assembly.FullName)
    ));

// Carter modules
builder.Services.AddCarter();

// SignalR
builder.Services.AddSignalR()
    .AddStackExchangeRedis(builder.Configuration.GetConnectionString("Redis") ?? "redis:6379",
        options => { options.Configuration.ChannelPrefix = "Village"; });

// OpenAPI
builder.Services.AddOpenApi();

// CORS (dev)
builder.Services.AddCors(options =>
{
    options.AddPolicy("Dev", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Middleware pipeline
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

app.MapCarter();

// Health check
app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    service = "village-api",
    version = "0.1.0",
    timestamp = DateTime.UtcNow
}));

// Apply migrations on startup (dev only — use CLI for prod)
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<VillageDbContext>();
    await db.Database.MigrateAsync();
}

await app.RunAsync();
