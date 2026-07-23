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
    public DateOnly? BirthDate { get; set; }
    public int PointsBalance { get; set; }
    public bool IsManaged { get; set; } // device-less child managed by parent
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Family Family { get; set; } = null!;
}
