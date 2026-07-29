namespace Village.Domain.Entities;

public class MealVote
{
    public Guid Id { get; set; }
    public Guid MealPlanEntryId { get; set; }
    public Guid FamilyMemberId { get; set; }
    public int Preference { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public MealPlanEntry MealPlanEntry { get; set; } = null!;
    public User FamilyMember { get; set; } = null!;
}
