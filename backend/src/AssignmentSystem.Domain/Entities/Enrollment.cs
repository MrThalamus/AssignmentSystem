namespace AssignmentSystem.Domain.Entities;

/// <summary>Places a student in a course. Drives which assignments a student can see.</summary>
public class Enrollment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CourseId { get; set; }
    public Course Course { get; set; } = null!;

    public Guid StudentId { get; set; }
    public User Student { get; set; } = null!;

    public DateTime EnrolledAt { get; set; }
}
