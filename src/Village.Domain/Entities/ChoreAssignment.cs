namespace Village.Domain.Entities;

public enum ChoreStatus
{
    Pending,
    InProgress,
    Completed,
    Missed,
    Waived
}

public class ChoreAssignment
{
    public Guid Id { get; set; }
    public Guid ChoreId { get; set; }
    public Guid AssignedToId { get; set; }
    public DateOnly DueDate { get; set; }
    public ChoreStatus Status { get; set; } = ChoreStatus.Pending;
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Chore Chore { get; set; } = null!;
    public User AssignedTo { get; set; } = null!;
    public ChoreCompletion? Completion { get; set; }
}
