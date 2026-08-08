using AssignmentSystem.Domain.Entities;
using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Domain.Exceptions;
using AssignmentSystem.Domain.ValueObjects;
using AssignmentSystem.Tests.TestSupport;

namespace AssignmentSystem.Tests.Domain;

/// <summary>
/// The submission lifecycle: handing in on time and late, revising, grading, and the
/// return-for-revision path that lets a student resubmit after a deadline.
/// </summary>
public class SubmissionRulesTests
{
    private static readonly DateTime Now = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid StudentId = Guid.NewGuid();
    private static readonly Guid TeacherId = Guid.NewGuid();

    private static Assignment Published(
        DateTime? deadline = null, bool allowLate = false, bool allowResubmit = true) => new()
    {
        Id = Guid.NewGuid(),
        Title = "Worksheet",
        Description = "Do the worksheet.",
        MaxMarks = 20m,
        Deadline = deadline ?? Now.AddDays(7),
        Status = AssignmentStatus.Published,
        AllowLateSubmission = allowLate,
        AllowResubmission = allowResubmit,
        CreatedAt = Now,
        PublishedAt = Now
    };

    /// <summary>
    /// A PDF named after the attempt it stands for, so tests can tell one attempt from
    /// the next by looking at the stored file name.
    /// </summary>
    private static SubmittedDocument Pdf(string fileName) =>
        new(fileName, TestPdf.Bytes(fileName));

    // ------------------------------------------------------------ handing in

    [Fact]
    public void Submitting_before_the_deadline_is_recorded_as_on_time()
    {
        var assignment = Published();

        var submission = Submission.Create(assignment, StudentId, Pdf("my-answer.pdf"), Now);

        Assert.Equal(SubmissionStatus.Submitted, submission.Status);
        Assert.False(submission.IsLate);
        Assert.Equal(1, submission.AttemptCount);
        Assert.Equal(Now, submission.SubmittedAt);
    }

    [Fact]
    public void Submitting_after_the_deadline_is_flagged_late_when_the_assignment_allows_it()
    {
        var assignment = Published(deadline: Now.AddDays(-1), allowLate: true);

        var submission = Submission.Create(assignment, StudentId, Pdf("late-answer.pdf"), Now);

        Assert.Equal(SubmissionStatus.Late, submission.Status);
        Assert.True(submission.IsLate);
    }

    [Fact]
    public void Submitting_after_the_deadline_is_refused_when_late_work_is_barred()
    {
        var assignment = Published(deadline: Now.AddDays(-1), allowLate: false);

        var error = Assert.Throws<BusinessRuleViolationException>(
            () => Submission.Create(assignment, StudentId, Pdf("too-late.pdf"), Now));

        Assert.Contains("deadline has passed", error.Message);
    }

    [Fact]
    public void Submitting_to_a_draft_assignment_is_refused()
    {
        var assignment = Published();
        assignment.Status = AssignmentStatus.Draft;

        var error = Assert.Throws<BusinessRuleViolationException>(
            () => Submission.Create(assignment, StudentId, Pdf("too-early.pdf"), Now));

        Assert.Contains("not open for submissions", error.Message);
    }

    [Fact]
    public void Submitting_to_a_closed_assignment_is_refused()
    {
        var assignment = Published(allowLate: true);
        assignment.Status = AssignmentStatus.Closed;

        Assert.Throws<BusinessRuleViolationException>(
            () => Submission.Create(assignment, StudentId, Pdf("answer.pdf"), Now));
    }

    // -------------------------------------------------------------- revising

    [Fact]
    public void A_student_may_revise_before_the_deadline()
    {
        var assignment = Published();
        var submission = Submission.Create(assignment, StudentId, Pdf("first-draft.pdf"), Now);

        submission.UpdateAnswer(assignment, Pdf("better-draft.pdf"), Now.AddHours(2));

        Assert.Equal("better-draft.pdf", submission.FileName);
        Assert.Equal(2, submission.AttemptCount);
        Assert.Equal(Now.AddHours(2), submission.UpdatedAt);
        Assert.False(submission.IsLate);
    }

    [Fact]
    public void Revising_is_refused_when_the_assignment_allows_only_one_attempt()
    {
        var assignment = Published(allowResubmit: false);
        var submission = Submission.Create(assignment, StudentId, Pdf("one-shot.pdf"), Now);

        var error = Assert.Throws<BusinessRuleViolationException>(
            () => submission.UpdateAnswer(assignment, Pdf("second-try.pdf"), Now.AddHours(1)));

        Assert.Contains("does not allow a submission to be updated", error.Message);
        Assert.Equal("one-shot.pdf", submission.FileName);
    }

    [Fact]
    public void Revising_after_the_deadline_is_refused_when_late_work_is_barred()
    {
        var assignment = Published(deadline: Now.AddHours(1));
        var submission = Submission.Create(assignment, StudentId, Pdf("first-draft.pdf"), Now);

        Assert.Throws<BusinessRuleViolationException>(
            () => submission.UpdateAnswer(assignment, Pdf("later-draft.pdf"), Now.AddDays(1)));
    }

    [Fact]
    public void A_revision_that_lands_after_the_deadline_becomes_late()
    {
        var assignment = Published(deadline: Now.AddHours(1), allowLate: true);
        var submission = Submission.Create(assignment, StudentId, Pdf("on-time.pdf"), Now);

        submission.UpdateAnswer(assignment, Pdf("now-overdue.pdf"), Now.AddDays(1));

        Assert.True(submission.IsLate);
        Assert.Equal(SubmissionStatus.Late, submission.Status);
    }

    [Fact]
    public void Graded_work_is_frozen()
    {
        var assignment = Published();
        var submission = Submission.Create(assignment, StudentId, Pdf("answer.pdf"), Now);
        submission.Grade(assignment, 15m, "Good.", TeacherId, Now.AddHours(1));

        var error = Assert.Throws<BusinessRuleViolationException>(
            () => submission.UpdateAnswer(assignment, Pdf("sneaky-edit.pdf"), Now.AddHours(2)));

        Assert.Contains("already been graded", error.Message);
        Assert.Equal("answer.pdf", submission.FileName);
    }

    [Fact]
    public void Work_returned_for_revision_can_be_resubmitted_even_after_the_deadline()
    {
        // The teacher asked for a redo, so the closed window should not block it.
        var assignment = Published(deadline: Now.AddHours(1), allowLate: false);
        var submission = Submission.Create(assignment, StudentId, Pdf("first-attempt.pdf"), Now);
        submission.ChangeStatus(SubmissionStatus.Returned, Now.AddHours(2));

        submission.UpdateAnswer(assignment, Pdf("revised-after-feedback.pdf"), Now.AddDays(3));

        Assert.Equal("revised-after-feedback.pdf", submission.FileName);
        Assert.Equal(SubmissionStatus.Submitted, submission.Status);
    }

    [Fact]
    public void Work_returned_for_revision_keeps_the_lateness_of_the_original_attempt()
    {
        var assignment = Published(deadline: Now.AddDays(-1), allowLate: true);
        var submission = Submission.Create(assignment, StudentId, Pdf("late-attempt.pdf"), Now);
        Assert.True(submission.IsLate);

        submission.ChangeStatus(SubmissionStatus.Returned, Now.AddHours(1));
        submission.UpdateAnswer(assignment, Pdf("revised.pdf"), Now.AddHours(2));

        Assert.True(submission.IsLate);
        Assert.Equal(SubmissionStatus.Late, submission.Status);
    }

    // -------------------------------------------------------------- grading

    [Fact]
    public void Grading_records_marks_feedback_and_the_grader()
    {
        var assignment = Published();
        var submission = Submission.Create(assignment, StudentId, Pdf("answer.pdf"), Now);

        submission.Grade(assignment, 17.5m, "Well argued.", TeacherId, Now.AddHours(3));

        Assert.Equal(SubmissionStatus.Graded, submission.Status);
        Assert.Equal(17.5m, submission.Marks);
        Assert.Equal("Well argued.", submission.Feedback);
        Assert.Equal(TeacherId, submission.GradedByTeacherId);
        Assert.Equal(Now.AddHours(3), submission.GradedAt);
    }

    [Fact]
    public void Marks_above_the_assignment_maximum_are_refused()
    {
        var assignment = Published();
        var submission = Submission.Create(assignment, StudentId, Pdf("answer.pdf"), Now);

        var error = Assert.Throws<BusinessRuleViolationException>(
            () => submission.Grade(assignment, 21m, null, TeacherId, Now));

        Assert.Contains("cannot exceed the maximum", error.Message);
        Assert.Null(submission.Marks);
        Assert.NotEqual(SubmissionStatus.Graded, submission.Status);
    }

    [Fact]
    public void Negative_marks_are_refused()
    {
        var assignment = Published();
        var submission = Submission.Create(assignment, StudentId, Pdf("answer.pdf"), Now);

        Assert.Throws<BusinessRuleViolationException>(
            () => submission.Grade(assignment, -1m, null, TeacherId, Now));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(20)]
    public void Marks_at_the_boundaries_are_accepted(int marks)
    {
        var assignment = Published();
        var submission = Submission.Create(assignment, StudentId, Pdf("answer.pdf"), Now);

        submission.Grade(assignment, marks, null, TeacherId, Now);

        Assert.Equal(marks, submission.Marks);
    }

    // -------------------------------------------------------- status changes

    [Fact]
    public void Returning_a_graded_submission_clears_the_previous_result()
    {
        var assignment = Published();
        var submission = Submission.Create(assignment, StudentId, Pdf("answer.pdf"), Now);
        submission.Grade(assignment, 12m, "Redo section 2.", TeacherId, Now.AddHours(1));

        submission.ChangeStatus(SubmissionStatus.Returned, Now.AddHours(2));

        Assert.Equal(SubmissionStatus.Returned, submission.Status);
        Assert.Null(submission.Marks);
        Assert.Null(submission.GradedAt);
        Assert.Null(submission.GradedByTeacherId);
    }

    [Fact]
    public void A_submission_cannot_be_flipped_to_graded_without_marks()
    {
        var assignment = Published();
        var submission = Submission.Create(assignment, StudentId, Pdf("answer.pdf"), Now);

        var error = Assert.Throws<BusinessRuleViolationException>(
            () => submission.ChangeStatus(SubmissionStatus.Graded, Now));

        Assert.Contains("without marks", error.Message);
    }
}
