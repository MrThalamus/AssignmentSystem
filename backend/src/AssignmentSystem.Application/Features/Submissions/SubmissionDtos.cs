using AssignmentSystem.Application.Common.Models;
using AssignmentSystem.Domain.Enums;

namespace AssignmentSystem.Application.Features.Submissions;

public record SubmissionDto(
    Guid Id,
    Guid AssignmentId,
    string AssignmentTitle,
    decimal AssignmentMaxMarks,
    DateTime AssignmentDeadline,
    Guid StudentId,
    string StudentName,
    string StudentEmail,
    string Content,
    string? AttachmentUrl,
    SubmissionStatus Status,
    bool IsLate,
    int AttemptCount,
    DateTime SubmittedAt,
    DateTime? UpdatedAt,
    decimal? Marks,
    string? Feedback,
    DateTime? GradedAt,
    string? GradedByTeacherName);

public record CreateSubmissionRequest(Guid AssignmentId, string Content, string? AttachmentUrl);

public record UpdateSubmissionRequest(string Content, string? AttachmentUrl);

public record GradeSubmissionRequest(decimal Marks, string? Feedback);

public record ChangeSubmissionStatusRequest(SubmissionStatus Status);

public class SubmissionListQuery : PaginationQuery
{
    public Guid? AssignmentId { get; set; }
    public Guid? CourseId { get; set; }
    public Guid? StudentId { get; set; }
    public SubmissionStatus? Status { get; set; }
}
