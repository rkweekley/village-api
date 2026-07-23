namespace Village.Domain.Entities;

public enum NotificationType
{
    ChoreAssigned,
    ChoreCompleted,
    ChoreApproved,
    ChoreRejected,
    RewardRedeemed,
    RewardApproved,
    RewardRejected,
    PointsChanged,
    FamilyMemberJoined,
    System
}

public enum NotificationPriority
{
    Low,
    Normal,
    High
}

public class Notification
{
    public Guid Id { get; set; }
    public Guid FamilyId { get; set; }
    public Guid UserId { get; set; }
    public NotificationType Type { get; set; }
    public NotificationPriority Priority { get; set; } = NotificationPriority.Normal;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? ReferenceId { get; set; }
    public string? ReferenceType { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }

    // Navigation
    public User User { get; set; } = null!;
}
