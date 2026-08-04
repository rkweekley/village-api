namespace Village.Domain.Entities;

public enum SchoolWorkStatus
{
    Pending,
    Submitted,
    Graded
}

public class SchoolWork
{
    public Guid Id { get; set; }
    public Guid FamilyId { get; set; }
    public Guid SubjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid AssignedToId { get; set; }
    public DateOnly DueDate { get; set; }
    public int PointsPossible { get; set; }
    public SchoolWorkStatus Status { get; set; } = SchoolWorkStatus.Pending;
    public string? SubmissionNote { get; set; }
    public int? PointsEarned { get; set; }
    public Guid? GradedById { get; set; }
    public DateTime? GradedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Family Family { get; set; } = null!;
    public SchoolSubject Subject { get; set; } = null!;
    public User AssignedTo { get; set; } = null!;
    public User? GradedBy { get; set; }
}
