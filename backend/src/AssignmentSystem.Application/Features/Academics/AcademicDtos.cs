using AssignmentSystem.Application.Common.Models;

namespace AssignmentSystem.Application.Features.Academics;

// ------------------------------------------------------------------- subjects

public record SubjectDto(Guid Id, string Name, string Code, string? Description);

public record SubjectRequest(string Name, string Code, string? Description);

// -------------------------------------------------------------------- courses

public record CourseDto(
    Guid Id,
    string Name,
    string Code,
    string AcademicYear,
    bool IsActive,
    int EnrolledStudentCount,
    int SubjectCount);

public record CourseRequest(string Name, string Code, string AcademicYear, bool IsActive);

public class CourseListQuery : PaginationQuery
{
    public bool? IsActive { get; set; }
    public string? Search { get; set; }
}

// ------------------------------------------------------------- course subjects

/// <summary>A subject taught within a course, plus the teacher responsible for it.</summary>
public record CourseSubjectDto(
    Guid Id,
    Guid CourseId,
    string CourseName,
    string CourseCode,
    Guid SubjectId,
    string SubjectName,
    string SubjectCode,
    Guid? TeacherId,
    string? TeacherName);

public record AddCourseSubjectRequest(Guid SubjectId, Guid? TeacherId);

public record AssignTeacherRequest(Guid? TeacherId);

// ---------------------------------------------------------------- enrollments

public record EnrollmentDto(
    Guid Id,
    Guid CourseId,
    Guid StudentId,
    string StudentName,
    string StudentEmail,
    DateTime EnrolledAt);

public record EnrollStudentsRequest(IReadOnlyList<Guid> StudentIds);
