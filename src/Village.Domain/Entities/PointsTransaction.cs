namespace Village.Domain.Entities;

public enum TransactionType
{
    ChoreEarned,
    BonusAwarded,
    RewardSpent,
    AllowancePayout,
    Adjustment
}

public class PointsTransaction
{
    public Guid Id { get; set; }
    public Guid FamilyId { get; set; }
    public Guid UserId { get; set; }
    public int Amount { get; set; }
    public int BalanceAfter { get; set; }
    public TransactionType Type { get; set; }
    public string? ReferenceId { get; set; } // FK to chore_completion, reward_redemption, etc.
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Family Family { get; set; } = null!;
    public User User { get; set; } = null!;
}
