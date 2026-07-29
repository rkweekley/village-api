using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Village.Domain.Entities;

namespace Village.Api.Services;

public interface IJwtService
{
    string GenerateToken(User user);
}

public class JwtService : IJwtService
{
    private readonly IConfiguration _configuration;

    public JwtService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateToken(User user)
    {
        var secret = _configuration["Jwt:Secret"];
        if (string.IsNullOrWhiteSpace(secret))
        {
            secret = Environment.GetEnvironmentVariable("JWT_SECRET");
        }
        if (string.IsNullOrWhiteSpace(secret))
        {
            secret = _configuration["Village__JwtSecret"];
        }
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new InvalidOperationException(
                "JWT signing key is not configured. Set the JWT_SECRET environment variable " +
                "(or Village__JwtSecret) to a secure key of at least 32 characters.");
        }
        var issuer = _configuration["Jwt:Issuer"] ?? "village.app";
        var audience = _configuration["Jwt:Audience"] ?? "village.app";

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Name, user.DisplayName),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim("family_id", user.FamilyId.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddDays(30),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
