namespace Village.Domain.Entities;

public class CalendarEvent
{
    public Guid Id { get; set; }
    public Guid FamilyId { get; set; }
    public Guid OrganizerId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Location { get; set; }
    public string? Color { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public bool IsAllDay { get; set; }
    public string? RecurrenceRule { get; set; } // RRULE format
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Family Family { get; set; } = null!;
    public User Organizer { get; set; } = null!;
    public ICollection<CalendarEventAttendee> Attendees { get; set; } = new List<CalendarEventAttendee>();
}
