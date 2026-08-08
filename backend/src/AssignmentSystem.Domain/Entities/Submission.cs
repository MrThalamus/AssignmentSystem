using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Domain.Exceptions;
using AssignmentSystem.Domain.ValueObjects;

namespace AssignmentSystem.Domain.Entities;

/// <summary>
/// One student's answer to one assignment, handed in as a PDF. A student has at most
/// one submission per assignment - re-submitting overwrites the existing row and bumps
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

    /// <summary>The name of the uploaded PDF, as shown to the student and the teacher.</summary>
    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = SubmittedDocument.PdfContentType;

    public int FileSizeBytes { get; set; }

    /// <summary>
    /// The bytes themselves. Kept off this entity so the row stays cheap to read;
    /// only the download endpoint ever loads it.
    /// </summary>
    public SubmissionFile? File { get; set; }

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
        SubmittedDocument document,
        DateTime utcNow)
    {
        if (!assignment.AcceptsSubmissionsAt(utcNow))
            throw new BusinessRuleViolationException(
                assignment.Status != AssignmentStatus.Published
                    ? "The assignment is not open for submissions."
                    : "The deadline has passed and this assignment does not allow late submissions.");

        var isLate = assignment.IsPastDeadline(utcNow);

        var submission = new Submission
        {
            AssignmentId = assignment.Id,
            StudentId = studentId,
            Status = isLate ? SubmissionStatus.Late : SubmissionStatus.Submitted,
            IsLate = isLate,
            AttemptCount = 1,
            SubmittedAt = utcNow
        };

        submission.AttachDocument(document);

        return submission;
    }

    /// <summary>
    /// Replaces the PDF with a newer attempt. Allowed while the assignment is still
    /// open, or at any time after the teacher returned the work for revision. Graded
    /// work is frozen - the teacher must return it first.
    /// </summary>
    public void UpdateAnswer(Assignment assignment, SubmittedDocument document, DateTime utcNow)
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

        AttachDocument(document);
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

    /// <summary>
    /// Points the submission at a new PDF. The existing row is rewritten rather than
    /// replaced so that a revision does not leave the previous attempt's bytes behind,
    /// and so EF sees an update rather than an insert on a key that already exists.
    /// </summary>
    private void AttachDocument(SubmittedDocument document)
    {
        FileName = document.FileName;
        ContentType = SubmittedDocument.PdfContentType;
        FileSizeBytes = document.SizeBytes;

        if (File is null)
            File = new SubmissionFile { SubmissionId = Id, Content = document.Content };
        else
            File.Content = document.Content;
    }
}
