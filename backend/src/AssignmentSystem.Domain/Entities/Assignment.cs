using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Domain.Exceptions;

namespace AssignmentSystem.Domain.Entities;

/// <summary>
/// A piece of work set by a teacher for one course/subject pairing.
/// The state transitions and submission-window rules live here rather than in the
/// service layer so that they hold no matter which entry point reaches them.
/// </summary>
public class Assignment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CourseSubjectId { get; set; }
    public CourseSubject CourseSubject { get; set; } = null!;

    /// <summary>The teacher who created it. Kept even if the course/subject is later reassigned.</summary>
    public Guid CreatedByTeacherId { get; set; }
    public User CreatedByTeacher { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal MaxMarks { get; set; }
    public DateTime Deadline { get; set; }
    public AssignmentStatus Status { get; set; } = AssignmentStatus.Draft;

    /// <summary>When false, submissions are rejected once the deadline passes.</summary>
    public bool AllowLateSubmission { get; set; }

    /// <summary>When false, a student gets exactly one shot: no edits after submitting.</summary>
    public bool AllowResubmission { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? PublishedAt { get; set; }

    public ICollection<Submission> Submissions { get; set; } = new List<Submission>();

    // ---------------------------------------------------------------- rules

    public bool IsVisibleToStudents => Status is AssignmentStatus.Published or AssignmentStatus.Closed;

    public bool IsPastDeadline(DateTime utcNow) => utcNow > Deadline;

    /// <summary>
    /// Whether the assignment is currently taking work. A closed assignment never is,
    /// and a published one stops at the deadline unless late submissions are allowed.
    /// </summary>
    public bool AcceptsSubmissionsAt(DateTime utcNow) =>
        Status == AssignmentStatus.Published && (!IsPastDeadline(utcNow) || AllowLateSubmission);

    /// <summary>
    /// Moves a draft to published. Publishing with a deadline already in the past is
    /// rejected - students would see work they can never hand in on time.
    /// </summary>
    public void Publish(DateTime utcNow)
    {
        if (Status == AssignmentStatus.Published)
            throw new BusinessRuleViolationException("The assignment is already published.");

        if (Status == AssignmentStatus.Closed)
            throw new BusinessRuleViolationException("A closed assignment cannot be published again.");

        if (Deadline <= utcNow)
            throw new BusinessRuleViolationException("The deadline must be in the future to publish an assignment.");

        Status = AssignmentStatus.Published;
        PublishedAt = utcNow;
        UpdatedAt = utcNow;
    }

    /// <summary>
    /// Pulls a published assignment back to draft. Refused once students have handed
    /// work in, because that would hide submissions they can already see.
    /// </summary>
    public void RevertToDraft(int submissionCount, DateTime utcNow)
    {
        if (Status != AssignmentStatus.Published)
            throw new BusinessRuleViolationException("Only a published assignment can be reverted to draft.");

        if (submissionCount > 0)
            throw new BusinessRuleViolationException(
                "The assignment already has submissions and can no longer be reverted to draft.");

        Status = AssignmentStatus.Draft;
        PublishedAt = null;
        UpdatedAt = utcNow;
    }

    /// <summary>Stops accepting work while leaving the assignment visible to students.</summary>
    public void Close(DateTime utcNow)
    {
        if (Status != AssignmentStatus.Published)
            throw new BusinessRuleViolationException("Only a published assignment can be closed.");

        Status = AssignmentStatus.Closed;
        UpdatedAt = utcNow;
    }

    /// <summary>
    /// Applies teacher edits. Marks cannot be re-scaled once grading has started,
    /// otherwise already-awarded marks could exceed the new maximum.
    /// </summary>
    public void UpdateDetails(
        string title,
        string description,
        decimal maxMarks,
        DateTime deadline,
        bool allowLateSubmission,
        bool allowResubmission,
        bool hasGradedSubmissions,
        DateTime utcNow)
    {
        if (Status == AssignmentStatus.Closed)
            throw new BusinessRuleViolationException("A closed assignment cannot be edited. Reopen it first.");

        if (maxMarks != MaxMarks && hasGradedSubmissions)
            throw new BusinessRuleViolationException(
                "Maximum marks cannot be changed after submissions have been graded.");

        Title = title;
        Description = description;
        MaxMarks = maxMarks;
        Deadline = deadline;
        AllowLateSubmission = allowLateSubmission;
        AllowResubmission = allowResubmission;
        UpdatedAt = utcNow;
    }

    /// <summary>
    /// Deletion is only safe while no student work depends on the assignment.
    /// Teachers are told to close it instead once submissions exist.
    /// </summary>
    public void EnsureCanBeDeleted(int submissionCount)
    {
        if (submissionCount > 0)
            throw new BusinessRuleViolationException(
                "The assignment has submissions and cannot be deleted. Close it instead.");
    }
}
