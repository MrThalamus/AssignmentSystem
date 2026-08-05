using AssignmentSystem.Domain.Enums;

namespace AssignmentSystem.Domain.Entities;

/// <summary>
/// A single account table backs all three roles. Admin, teacher and student share
/// the same identity fields, so splitting them into separate tables would only add
/// joins without adding information. Role-specific data lives in the link tables
/// (<see cref="CourseSubject.TeacherId"/> and <see cref="Enrollment"/>).
/// </summary>
public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    /// <summary>Course/subject pairings this user teaches (teachers only).</summary>
    public ICollection<CourseSubject> TeachingAssignments { get; set; } = new List<CourseSubject>();

    /// <summary>Courses this user is enrolled in (students only).</summary>
    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();

    /// <summary>Submissions authored by this user (students only).</summary>
    public ICollection<Submission> Submissions { get; set; } = new List<Submission>();
}
