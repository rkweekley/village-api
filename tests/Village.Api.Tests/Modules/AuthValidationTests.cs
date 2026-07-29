using Village.Api.Dtos.Auth;
using Village.Api.Validators.Auth;

namespace Village.Api.Tests.Modules;

public class AuthValidationTests
{
    [Fact]
    public void RegisterRequest_ValidData_PassesValidation()
    {
        var validator = new RegisterRequestValidator();
        var request = new RegisterRequest(
            "user@example.com",
            "TestUser",
            "password123!",
            null
        );

        var result = validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void RegisterRequest_InvalidEmail_FailsValidation()
    {
        var validator = new RegisterRequestValidator();
        var request = new RegisterRequest(
            "not-an-email",
            "TestUser",
            "password123!",
            null
        );

        var result = validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Email");
    }

    [Fact]
    public void RegisterRequest_ShortPassword_FailsValidation()
    {
        var validator = new RegisterRequestValidator();
        var request = new RegisterRequest(
            "user@example.com",
            "TestUser",
            "1234567", // 7 chars — min is 8
            null
        );

        var result = validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Password");
    }

    [Fact]
    public void RegisterRequest_EmptyDisplayName_FailsValidation()
    {
        var validator = new RegisterRequestValidator();
        var request = new RegisterRequest(
            "user@example.com",
            "",
            "password123!",
            null
        );

        var result = validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "DisplayName");
    }

    [Fact]
    public void LoginRequest_ValidData_PassesValidation()
    {
        var validator = new LoginRequestValidator();
        var request = new LoginRequest("user@example.com", "password");

        var result = validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void LoginRequest_InvalidEmail_FailsValidation()
    {
        var validator = new LoginRequestValidator();
        var request = new LoginRequest("bad", "");

        var result = validator.Validate(request);

        Assert.False(result.IsValid);
    }
}
