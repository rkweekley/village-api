namespace Village.Domain.Entities;

public class Recipe
{
    public Guid Id { get; set; }
    public Guid FamilyId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Ingredients { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;
    public int PrepTimeMinutes { get; set; }
    public int Servings { get; set; } = 4;
    public string Difficulty { get; set; } = "Easy";
    public string Tags { get; set; } = string.Empty;
    public string? PhotoUrl { get; set; }
    public bool IsFamilyFavorite { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid CreatedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Family Family { get; set; } = null!;
    public User CreatedBy { get; set; } = null!;
}
