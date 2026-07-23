namespace Village.Domain.Entities;

public enum RedemptionStatus
{
    Pending,
    Approved,
    Rejected,
    Fulfilled
}

public class RewardRedemption
{
    public Guid Id { get; set; }
    public Guid RewardId { get; set; }
    public Guid UserId { get; set; }
    public Guid? ApprovedById { get; set; }
    public int PointsCost { get; set; }
    public RedemptionStatus Status { get; set; } = RedemptionStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ApprovedAt { get; set; }

    public Reward Reward { get; set; } = null!;
    public User User { get; set; } = null!;
    public User? ApprovedBy { get; set; }
}
