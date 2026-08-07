namespace Village.Api.Dtos.Auth;

public record ContactRequest(
    string Name,
    string Email,
    string Subject,
    string Message
);
