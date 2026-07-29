namespace Village.Domain.Entities;

public enum ChoreRecurrence
{
    Once,
    Daily,
    Weekly,
    Biweekly,
    Monthly,
    CustomCron
}

public enum ChoreDifficulty
{
    Easy,
    Medium,
    Hard
}

public class Chore
{
    public Guid Id { get; set; }
    public Guid FamilyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int PointValue { get; set; } = 10;
    public ChoreRecurrence Recurrence { get; set; } = ChoreRecurrence.Once;
    public ChoreDifficulty Difficulty { get; set; } = ChoreDifficulty.Easy;
    public bool RequiresApproval { get; set; } = true;
    public bool RequiresPhoto { get; set; }
    public string? CronExpression { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid? CreatedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Family Family { get; set; } = null!;
    public User? Creator { get; set; }
    public ICollection<ChoreAssignment> Assignments { get; set; } = new List<ChoreAssignment>();
}
