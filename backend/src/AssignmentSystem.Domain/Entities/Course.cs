namespace AssignmentSystem.Domain.Entities;

/// <summary>
/// A class or course - the group of students an assignment is handed to,
/// e.g. "Grade 10 - Section A" or "CSE 2nd Semester".
/// </summary>
public class Course
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string AcademicYear { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }

    public ICollection<CourseSubject> CourseSubjects { get; set; } = new List<CourseSubject>();
    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
}
