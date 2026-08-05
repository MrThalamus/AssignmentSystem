using AssignmentSystem.Domain.Entities;
using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Domain.Exceptions;

namespace AssignmentSystem.Tests.Domain;

/// <summary>
/// The assignment lifecycle rules, exercised directly on the entity. These hold no
/// matter which service or endpoint reaches them, so they are worth pinning here
/// rather than only through the API.
/// </summary>
public class AssignmentRulesTests
{
    private static readonly DateTime Now = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);

    private static Assignment Draft(DateTime? deadline = null) => new()
    {
        Title = "Worksheet",
        Description = "Do the worksheet.",
        MaxMarks = 20m,
        Deadline = deadline ?? Now.AddDays(7),
        Status = AssignmentStatus.Draft,
        CreatedAt = Now
    };

    // ------------------------------------------------------------ publishing

    [Fact]
    public void Publish_makes_the_assignment_visible_and_records_the_time()
    {
        var assignment = Draft();

        assignment.Publish(Now);

        Assert.Equal(AssignmentStatus.Published, assignment.Status);
        Assert.Equal(Now, assignment.PublishedAt);
        Assert.True(assignment.IsVisibleToStudents);
    }

    [Fact]
    public void Publish_is_refused_when_the_deadline_has_already_passed()
    {
        var assignment = Draft(deadline: Now.AddDays(-1));

        var error = Assert.Throws<BusinessRuleViolationException>(() => assignment.Publish(Now));

        Assert.Contains("must be in the future", error.Message);
        Assert.Equal(AssignmentStatus.Draft, assignment.Status);
    }

    [Fact]
    public void Publish_is_refused_when_the_deadline_is_exactly_now()
    {
        var assignment = Draft(deadline: Now);

        Assert.Throws<BusinessRuleViolationException>(() => assignment.Publish(Now));
    }

    [Fact]
    public void Publishing_twice_is_refused()
    {
        var assignment = Draft();
        assignment.Publish(Now);

        Assert.Throws<BusinessRuleViolationException>(() => assignment.Publish(Now));
    }

    [Fact]
    public void A_draft_is_not_visible_to_students()
    {
        Assert.False(Draft().IsVisibleToStudents);
    }

    // ----------------------------------------------------- reverting to draft

    [Fact]
    public void Revert_to_draft_hides_a_published_assignment_that_has_no_submissions()
    {
        var assignment = Draft();
        assignment.Publish(Now);

        assignment.RevertToDraft(submissionCount: 0, Now);

        Assert.Equal(AssignmentStatus.Draft, assignment.Status);
        Assert.Null(assignment.PublishedAt);
    }

    [Fact]
    public void Revert_to_draft_is_refused_once_students_have_submitted()
    {
        var assignment = Draft();
        assignment.Publish(Now);

        var error = Assert.Throws<BusinessRuleViolationException>(
            () => assignment.RevertToDraft(submissionCount: 1, Now));

        Assert.Contains("already has submissions", error.Message);
        Assert.Equal(AssignmentStatus.Published, assignment.Status);
    }

    // --------------------------------------------------------------- closing

    [Fact]
    public void Close_stops_submissions_but_leaves_the_assignment_readable()
    {
        var assignment = Draft();
        assignment.Publish(Now);

        assignment.Close(Now);

        Assert.Equal(AssignmentStatus.Closed, assignment.Status);
        Assert.True(assignment.IsVisibleToStudents);
        Assert.False(assignment.AcceptsSubmissionsAt(Now));
    }

    [Fact]
    public void A_draft_cannot_be_closed()
    {
        Assert.Throws<BusinessRuleViolationException>(() => Draft().Close(Now));
    }

    [Fact]
    public void A_closed_assignment_cannot_be_published_again()
    {
        var assignment = Draft();
        assignment.Publish(Now);
        assignment.Close(Now);

        Assert.Throws<BusinessRuleViolationException>(() => assignment.Publish(Now));
    }

    // ------------------------------------------------------ submission window

    [Fact]
    public void A_published_assignment_accepts_submissions_before_the_deadline()
    {
        var assignment = Draft();
        assignment.Publish(Now);

        Assert.True(assignment.AcceptsSubmissionsAt(Now.AddDays(6)));
    }

    [Fact]
    public void A_published_assignment_stops_accepting_at_the_deadline_when_late_work_is_barred()
    {
        var assignment = Draft();
        assignment.Publish(Now);
        assignment.AllowLateSubmission = false;

        Assert.False(assignment.AcceptsSubmissionsAt(Now.AddDays(7).AddSeconds(1)));
    }

    [Fact]
    public void A_published_assignment_keeps_accepting_past_the_deadline_when_late_work_is_allowed()
    {
        var assignment = Draft();
        assignment.Publish(Now);
        assignment.AllowLateSubmission = true;

        Assert.True(assignment.AcceptsSubmissionsAt(Now.AddDays(30)));
    }

    [Fact]
    public void A_draft_never_accepts_submissions()
    {
        Assert.False(Draft().AcceptsSubmissionsAt(Now));
    }

    // --------------------------------------------------------------- editing

    [Fact]
    public void Editing_applies_the_new_details()
    {
        var assignment = Draft();

        assignment.UpdateDetails(
            "New title", "New description", 30m, Now.AddDays(10),
            allowLateSubmission: true, allowResubmission: false,
            hasGradedSubmissions: false, Now);

        Assert.Equal("New title", assignment.Title);
        Assert.Equal(30m, assignment.MaxMarks);
        Assert.True(assignment.AllowLateSubmission);
        Assert.False(assignment.AllowResubmission);
        Assert.Equal(Now, assignment.UpdatedAt);
    }

    [Fact]
    public void Maximum_marks_cannot_be_changed_after_grading_has_started()
    {
        var assignment = Draft();
        assignment.Publish(Now);

        var error = Assert.Throws<BusinessRuleViolationException>(() => assignment.UpdateDetails(
            assignment.Title, assignment.Description, 50m, assignment.Deadline,
            assignment.AllowLateSubmission, assignment.AllowResubmission,
            hasGradedSubmissions: true, Now));

        Assert.Contains("Maximum marks", error.Message);
        Assert.Equal(20m, assignment.MaxMarks);
    }

    [Fact]
    public void Other_details_stay_editable_after_grading_has_started()
    {
        var assignment = Draft();
        assignment.Publish(Now);

        assignment.UpdateDetails(
            "Clarified title", "Clarified description", assignment.MaxMarks, Now.AddDays(9),
            assignment.AllowLateSubmission, assignment.AllowResubmission,
            hasGradedSubmissions: true, Now);

        Assert.Equal("Clarified title", assignment.Title);
    }

    [Fact]
    public void A_closed_assignment_cannot_be_edited()
    {
        var assignment = Draft();
        assignment.Publish(Now);
        assignment.Close(Now);

        Assert.Throws<BusinessRuleViolationException>(() => assignment.UpdateDetails(
            "New title", "New description", 20m, Now.AddDays(10),
            false, true, hasGradedSubmissions: false, Now));
    }

    // -------------------------------------------------------------- deletion

    [Fact]
    public void Deleting_an_assignment_with_no_submissions_is_allowed()
    {
        Draft().EnsureCanBeDeleted(submissionCount: 0);
    }

    [Fact]
    public void Deleting_an_assignment_that_has_submissions_is_refused()
    {
        var error = Assert.Throws<BusinessRuleViolationException>(
            () => Draft().EnsureCanBeDeleted(submissionCount: 3));

        Assert.Contains("Close it instead", error.Message);
    }
}
