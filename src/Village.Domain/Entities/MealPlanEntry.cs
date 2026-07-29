namespace Village.Domain.Entities;

public class MealPlanEntry
{
    public Guid Id { get; set; }
    public Guid MealPlanId { get; set; }
    public int DayOfWeek { get; set; }
    public string MealType { get; set; } = string.Empty;
    public Guid? RecipeId { get; set; }
    public string? Title { get; set; }
    public int SortOrder { get; set; }

    public MealPlan MealPlan { get; set; } = null!;
    public Recipe? Recipe { get; set; }
    public ICollection<MealVote> Votes { get; set; } = new List<MealVote>();
}
