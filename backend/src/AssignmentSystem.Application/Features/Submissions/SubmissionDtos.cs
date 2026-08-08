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
    string FileName,
    string ContentType,
    int FileSizeBytes,
    SubmissionStatus Status,
    bool IsLate,
    int AttemptCount,
    DateTime SubmittedAt,
    DateTime? UpdatedAt,
    decimal? Marks,
    string? Feedback,
    DateTime? GradedAt,
    string? GradedByTeacherName);

/// <summary>
/// An uploaded file as the application layer sees it - already buffered, and stripped
/// of any dependency on ASP.NET's <c>IFormFile</c> so the service stays testable.
/// </summary>
/// <param name="FileName">The name as the browser reported it; sanitised before storage.</param>
/// <param name="ContentType">The browser's claim about the type, which is checked but not trusted.</param>
public record UploadedFile(string FileName, string? ContentType, byte[] Content);

/// <summary>The bytes of a stored submission, ready to be streamed back to the caller.</summary>
public record SubmissionFileDto(string FileName, string ContentType, byte[] Content);

public record CreateSubmissionRequest(Guid AssignmentId, UploadedFile? File);

public record UpdateSubmissionRequest(UploadedFile? File);

public record GradeSubmissionRequest(decimal Marks, string? Feedback);

public record ChangeSubmissionStatusRequest(SubmissionStatus Status);

public class SubmissionListQuery : PaginationQuery
{
    public Guid? AssignmentId { get; set; }
    public Guid? CourseId { get; set; }
    public Guid? StudentId { get; set; }
    public SubmissionStatus? Status { get; set; }
}
