namespace Village.Domain.Entities;

public enum RewardCategory
{
    ScreenTime,
    Outing,
    Treat,
    Allowance,
    Custom
}

public class Reward
{
    public Guid Id { get; set; }
    public Guid FamilyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int PointCost { get; set; }
    public RewardCategory Category { get; set; } = RewardCategory.Custom;
    public int? MaxRedemptions { get; set; }
    public bool RequiresApproval { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Family Family { get; set; } = null!;
    public ICollection<RewardRedemption> Redemptions { get; set; } = new List<RewardRedemption>();
}
