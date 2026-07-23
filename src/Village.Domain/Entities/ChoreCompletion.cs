namespace Village.Domain.Entities;

public enum ApprovalStatus
{
    Pending,
    Approved,
    Rejected
}

public class ChoreCompletion
{
    public Guid Id { get; set; }
    public Guid ChoreAssignmentId { get; set; }
    public Guid CompletedById { get; set; }
    public Guid? ApprovedById { get; set; }
    public string? EvidencePhotoUrl { get; set; }
    public string? Note { get; set; }
    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Pending;
    public int PointsAwarded { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ApprovedAt { get; set; }

    public ChoreAssignment Assignment { get; set; } = null!;
    public User CompletedBy { get; set; } = null!;
    public User? ApprovedBy { get; set; }
}
