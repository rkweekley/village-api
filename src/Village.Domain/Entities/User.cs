namespace Village.Domain.Entities;

public enum UserRole
{
    Parent,
    Child,
    Caregiver
}

public class User
{
    public Guid Id { get; set; }
    public Guid FamilyId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Child;
    public string PasswordHash { get; set; } = string.Empty;
    public DateOnly? BirthDate { get; set; }
    public int PointsBalance { get; set; }
    public bool IsManaged { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiresAt { get; set; }
    public string? PasswordResetToken { get; set; }
    public DateTime? PasswordResetTokenExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Family Family { get; set; } = null!;
}
