namespace Village.Api.Dtos.Auth;

public record RegisterRequest(
    string Email,
    string DisplayName,
    string Password,
    string? InviteCode
);

public record LoginRequest(
    string Email,
    string Password
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
    string AccessToken,
    string RefreshToken
);

public record ForgotPasswordRequest(
    string Email
);

public record ResetPasswordRequest(
    string Token,
    string Email,
    string NewPassword
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
