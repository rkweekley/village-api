using System.ComponentModel.DataAnnotations;

namespace Village.Api.Dtos.Auth;

public record RegisterRequest(
    [Required] [EmailAddress] string Email,
    [Required] [MinLength(3)] string DisplayName,
    [Required] [MinLength(8)] string Password,
    string? InviteCode // null = creating a new family
);

public record LoginRequest(
    [Required] [EmailAddress] string Email,
    [Required] string Password
);

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    Guid UserId,
    string DisplayName,
    string Email,
    string Role,
    Guid FamilyId,
    string FamilyName,
    bool IsNewFamily
);

public record RefreshRequest(
    [Required] string AccessToken,
    [Required] string RefreshToken
);

public record ForgotPasswordRequest(
    [Required] [EmailAddress] string Email
);

public record ResetPasswordRequest(
    [Required] string Token,
    [Required] [MinLength(8)] string NewPassword
);

public record UserInfoResponse(
    Guid Id,
    string DisplayName,
    string Email,
    string Role,
    int PointsBalance,
    DateOnly? BirthDate,
    Guid FamilyId
);
