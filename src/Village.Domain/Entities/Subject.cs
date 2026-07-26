namespace Village.Domain.Entities;

public class Subject
{
    public Guid Id { get; set; }
    public Guid FamilyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Color { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Family Family { get; set; } = null!;
    public ICollection<SchoolWork> SchoolWorks { get; set; } = new List<SchoolWork>();
}
