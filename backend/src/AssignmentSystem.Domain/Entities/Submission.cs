using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Domain.Exceptions;

namespace AssignmentSystem.Domain.Entities;

/// <summary>
/// One student's answer to one assignment. A student has at most one submission per
/// assignment - re-submitting overwrites the existing row and bumps
/// <see cref="AttemptCount"/> rather than creating a second record, which keeps
/// "the student's answer" unambiguous when grading.
/// </summary>
public class Submission
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid AssignmentId { get; set; }
    public Assignment Assignment { get; set; } = null!;

    public Guid StudentId { get; set; }
    public User Student { get; set; } = null!;

    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Link to work hosted elsewhere. File storage is out of scope for this project,
    /// so the API accepts a URL rather than a binary upload.
    /// </summary>
    public string? AttachmentUrl { get; set; }

    public SubmissionStatus Status { get; set; } = SubmissionStatus.Submitted;
    public bool IsLate { get; set; }
    public int AttemptCount { get; set; } = 1;

    public DateTime SubmittedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public decimal? Marks { get; set; }
    public string? Feedback { get; set; }
    public DateTime? GradedAt { get; set; }

    public Guid? GradedByTeacherId { get; set; }
    public User? GradedByTeacher { get; set; }

    // ---------------------------------------------------------------- rules

    /// <summary>
    /// Creates the first attempt. The caller is responsible for having checked that
    /// the assignment accepts submissions; this only records whether it arrived late.
    /// </summary>
    public static Submission Create(
        Assignment assignment,
        Guid studentId,
        string content,
        string? attachmentUrl,
        DateTime utcNow)
    {
        if (!assignment.AcceptsSubmissionsAt(utcNow))
            throw new BusinessRuleViolationException(
                assignment.Status != AssignmentStatus.Published
                    ? "The assignment is not open for submissions."
                    : "The deadline has passed and this assignment does not allow late submissions.");

        var isLate = assignment.IsPastDeadline(utcNow);

        return new Submission
        {
            AssignmentId = assignment.Id,
            StudentId = studentId,
            Content = content,
            AttachmentUrl = attachmentUrl,
            Status = isLate ? SubmissionStatus.Late : SubmissionStatus.Submitted,
            IsLate = isLate,
            AttemptCount = 1,
            SubmittedAt = utcNow
        };
    }

    /// <summary>
    /// Replaces the answer with a newer attempt. Allowed while the assignment is still
    /// open, or at any time after the teacher returned the work for revision. Graded
    /// work is frozen - the teacher must return it first.
    /// </summary>
    public void UpdateAnswer(Assignment assignment, string content, string? attachmentUrl, DateTime utcNow)
    {
        if (Status == SubmissionStatus.Graded)
            throw new BusinessRuleViolationException(
                "The submission has already been graded and can no longer be changed.");

        var revisionRequested = Status == SubmissionStatus.Returned;

        if (!revisionRequested)
        {
            if (!assignment.AllowResubmission)
                throw new BusinessRuleViolationException(
                    "This assignment does not allow a submission to be updated.");

            if (!assignment.AcceptsSubmissionsAt(utcNow))
                throw new BusinessRuleViolationException(
                    "The deadline has passed and this assignment does not allow late submissions.");
        }

        Content = content;
        AttachmentUrl = attachmentUrl;
        AttemptCount++;
        UpdatedAt = utcNow;

        // A revision that lands after the deadline is late, but work the teacher asked
        // to be redone keeps whatever lateness the original attempt had.
        if (!revisionRequested)
            IsLate = assignment.IsPastDeadline(utcNow);

        Status = IsLate ? SubmissionStatus.Late : SubmissionStatus.Submitted;
    }

    /// <summary>Records marks and feedback. Marks are bounded by the assignment's maximum.</summary>
    public void Grade(Assignment assignment, decimal marks, string? feedback, Guid teacherId, DateTime utcNow)
    {
        if (marks < 0)
            throw new BusinessRuleViolationException("Marks cannot be negative.");

        if (marks > assignment.MaxMarks)
            throw new BusinessRuleViolationException(
                $"Marks cannot exceed the maximum of {assignment.MaxMarks} for this assignment.");

        Marks = marks;
        Feedback = feedback;
        Status = SubmissionStatus.Graded;
        GradedAt = utcNow;
        GradedByTeacherId = teacherId;
        UpdatedAt = utcNow;
    }

    /// <summary>
    /// Lets a teacher move a submission between states by hand, e.g. returning graded
    /// work for revision. Grading is not reachable this way because it needs marks.
    /// </summary>
    public void ChangeStatus(SubmissionStatus newStatus, DateTime utcNow)
    {
        if (newStatus == SubmissionStatus.Graded && Marks is null)
            throw new BusinessRuleViolationException(
                "A submission cannot be marked as graded without marks. Grade it instead.");

        if (newStatus == SubmissionStatus.Returned)
        {
            // Returning clears the previous result so the next attempt is graded fresh.
            Marks = null;
            GradedAt = null;
            GradedByTeacherId = null;
        }

        Status = newStatus;
        UpdatedAt = utcNow;
    }
}
