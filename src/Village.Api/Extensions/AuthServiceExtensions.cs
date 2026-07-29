using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Village.Api.Extensions;

public static class AuthServiceExtensions
{
    public static IServiceCollection AddVillageAuth(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET")
                        ?? configuration["Village__JwtSecret"]
                        ?? throw new InvalidOperationException(
                            "JWT_SECRET environment variable is not set. Set JWT_SECRET (or Village__JwtSecret) " +
                            "to a secure key of at least 32 characters before starting the application.");
        var jwtIssuer = configuration["Jwt:Issuer"] ?? "village.app";
        var jwtAudience = configuration["Jwt:Audience"] ?? "village.app";

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtIssuer,
                ValidAudience = jwtAudience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
            };

            // Allow SignalR to receive JWT via query string
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    var path = context.HttpContext.Request.Path;
                    if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                    {
                        context.Token = accessToken;
                    }
                    return Task.CompletedTask;
                }
            };
        });

        services.AddAuthorization();

        return services;
    }
}
