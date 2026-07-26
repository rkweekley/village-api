namespace Village.Domain.Entities;

public class MealPlan
{
    public Guid Id { get; set; }
    public Guid FamilyId { get; set; }
    public DateOnly WeekStart { get; set; }
    public DateOnly WeekEnd { get; set; }
    public Guid CreatedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Family Family { get; set; } = null!;
    public User CreatedBy { get; set; } = null!;
    public ICollection<MealPlanEntry> Entries { get; set; } = new List<MealPlanEntry>();
}
