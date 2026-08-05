namespace AssignmentSystem.Domain.Entities;

/// <summary>A teachable subject, e.g. "Mathematics" or "Data Structures".</summary>
public class Subject
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<CourseSubject> CourseSubjects { get; set; } = new List<CourseSubject>();
}
