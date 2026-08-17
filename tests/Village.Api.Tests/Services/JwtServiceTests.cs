using Microsoft.Extensions.Configuration;
using Village.Api.Services;
using Village.Domain.Entities;

namespace Village.Api.Tests.Services;

public class JwtServiceTests
{
    private static IConfiguration CreateConfig(string secret)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Village__JwtSecret"] = secret,
                ["Jwt:Issuer"] = "test.village.app",
                ["Jwt:Audience"] = "test.village.app"
            })!
            .Build();
    }

    [Fact]
    public void GenerateAccessToken_ValidUserAndSecret_ReturnsToken()
    {
        // Arrange
        var config = CreateConfig("this-is-a-test-secret-that-is-long-enough-32!");
        var service = new JwtService(config);
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            DisplayName = "Test User",
            Role = UserRole.Parent,
            FamilyId = Guid.NewGuid()
        };

        // Act
        var token = service.GenerateAccessToken(user);

        // Assert
        Assert.NotNull(token);
        Assert.NotEmpty(token);
        Assert.Contains(".", token); // JWT has three dot-separated parts
    }

    [Fact]
    public void GenerateAccessToken_InvalidUser_ThrowsArgumentNullException()
    {
        // Arrange
        var config = CreateConfig("this-is-a-test-secret-that-is-long-enough-32!");
        var service = new JwtService(config);

        // Act & Assert
        Assert.Throws<NullReferenceException>(() => service.GenerateAccessToken(null!));
    }

    [Fact]
    public void GenerateAccessToken_EmptySecret_ThrowsInvalidOperationException()
    {
        // Arrange
        var config = CreateConfig("");
        var service = new JwtService(config);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => service.GenerateAccessToken(new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            DisplayName = "Test",
            Role = UserRole.Child,
            FamilyId = Guid.NewGuid()
        }));
    }

    [Fact]
    public void GenerateAccessToken_IncludesUserClaims()
    {
        // Arrange
        var config = CreateConfig("this-is-a-test-secret-that-is-long-enough-32!!");
        var service = new JwtService(config);
        var userId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = "child@family.com",
            DisplayName = "Kid",
            Role = UserRole.Child,
            FamilyId = familyId
        };

        // Act
        var token = service.GenerateAccessToken(user);

        // Assert
        Assert.NotNull(token);
        Assert.NotEmpty(token);
    }
}
