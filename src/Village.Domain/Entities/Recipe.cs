namespace Village.Domain.Entities;

public enum RecipeDifficulty
{
    Easy,
    Medium,
    Hard
}

public class Recipe
{
    public Guid Id { get; set; }
    public Guid FamilyId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Ingredients { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;
    public int PrepTimeMinutes { get; set; } = 30;
    public int Servings { get; set; } = 4;
    public RecipeDifficulty Difficulty { get; set; } = RecipeDifficulty.Easy;
    public string? Tags { get; set; }
    public string? PhotoUrl { get; set; }
    public bool IsFamilyFavorite { get; set; }
    public Guid CreatedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Family Family { get; set; } = null!;
    public User CreatedBy { get; set; } = null!;
}
