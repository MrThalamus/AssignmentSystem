using AssignmentSystem.Application.Common.Models;
using AssignmentSystem.Domain.Enums;

namespace AssignmentSystem.Application.Features.Assignments;

public record AssignmentDto(
    Guid Id,
    string Title,
    string Description,
    decimal MaxMarks,
    DateTime Deadline,
    AssignmentStatus Status,
    bool AllowLateSubmission,
    bool AllowResubmission,
    DateTime CreatedAt,
    DateTime? PublishedAt,
    Guid CourseSubjectId,
    Guid CourseId,
    string CourseName,
    string CourseCode,
    Guid SubjectId,
    string SubjectName,
    Guid TeacherId,
    string TeacherName,
    int SubmissionCount,
    int GradedCount,
    // Only populated for students: their own submission, if any.
    StudentSubmissionSummary? MySubmission);

/// <summary>Just enough of a student's own submission to render an assignment card.</summary>
public record StudentSubmissionSummary(
    Guid Id,
    SubmissionStatus Status,
    bool IsLate,
    DateTime SubmittedAt,
    decimal? Marks);

public record CreateAssignmentRequest(
    Guid CourseSubjectId,
    string Title,
    string Description,
    decimal MaxMarks,
    DateTime Deadline,
    bool AllowLateSubmission,
    bool AllowResubmission,
    // Publish immediately instead of saving as a draft.
    bool PublishNow);

public record UpdateAssignmentRequest(
    string Title,
    string Description,
    decimal MaxMarks,
    DateTime Deadline,
    bool AllowLateSubmission,
    bool AllowResubmission);

public class AssignmentListQuery : PaginationQuery
{
    public Guid? CourseId { get; set; }
    public Guid? SubjectId { get; set; }
    public AssignmentStatus? Status { get; set; }
    public string? Search { get; set; }

    /// <summary>Students only: restrict to assignments they have not submitted yet.</summary>
    public bool? OnlyPending { get; set; }

    public DateTime? DueBefore { get; set; }
    public DateTime? DueAfter { get; set; }
}
