namespace Village.Domain.Entities;

public enum AttendeeStatus
{
    Pending,
    Accepted,
    Declined,
    Tentative
}

public class CalendarEventAttendee
{
    public Guid EventId { get; set; }
    public Guid UserId { get; set; }
    public AttendeeStatus Status { get; set; } = AttendeeStatus.Pending;

    public CalendarEvent Event { get; set; } = null!;
    public User User { get; set; } = null!;
}
