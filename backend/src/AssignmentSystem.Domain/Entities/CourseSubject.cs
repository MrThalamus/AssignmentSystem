namespace AssignmentSystem.Domain.Entities;

/// <summary>
/// Joins a <see cref="Course"/> to a <see cref="Subject"/> and names the teacher
/// responsible for that pairing. This is the unit a teacher actually owns: "Maths
/// for Grade 10-A". Every assignment hangs off one of these rows, which is what
/// makes teacher ownership and student visibility a single lookup instead of a
/// guess spread across two independent relationships.
/// </summary>
public class CourseSubject
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CourseId { get; set; }
    public Course Course { get; set; } = null!;

    public Guid SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;

    /// <summary>
    /// Null until an admin assigns a teacher. Assignments cannot be created for an
    /// unassigned pairing.
    /// </summary>
    public Guid? TeacherId { get; set; }
    public User? Teacher { get; set; }

    public DateTime CreatedAt { get; set; }

    public ICollection<Assignment> Assignments { get; set; } = new List<Assignment>();
}
