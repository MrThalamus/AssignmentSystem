using AssignmentSystem.Application.Common.Exceptions;
using AssignmentSystem.Application.Features.Assignments;
using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Domain.Exceptions;
using AssignmentSystem.Tests.TestSupport;

namespace AssignmentSystem.Tests.Services;

/// <summary>Creating, publishing and retiring assignments through the service.</summary>
public class AssignmentWorkflowTests
{
    private static CreateAssignmentRequest NewAssignment(
        Guid courseSubjectId, DateTime deadline, bool publishNow = false) =>
        new(courseSubjectId, "Chapter 5 problems", "Answer every question in chapter 5.",
            25m, deadline, AllowLateSubmission: false, AllowResubmission: true, publishNow);

    [Fact]
    public async Task A_new_assignment_starts_as_a_draft()
    {
        using var world = new TestWorld().AsTeacherA();

        var created = await world.Assignments()
            .CreateAsync(NewAssignment(world.MathsCourseSubjectId, TestWorld.Start.AddDays(5)));

        Assert.Equal(AssignmentStatus.Draft, created.Status);
        Assert.Null(created.PublishedAt);
        Assert.Equal(world.TeacherAId, created.TeacherId);
    }

    [Fact]
    public async Task Creating_with_publish_now_makes_it_visible_immediately()
    {
        using var world = new TestWorld().AsTeacherA();

        var created = await world.Assignments()
            .CreateAsync(NewAssignment(world.MathsCourseSubjectId, TestWorld.Start.AddDays(5), publishNow: true));

        Assert.Equal(AssignmentStatus.Published, created.Status);
        Assert.Equal(TestWorld.Start, created.PublishedAt);
    }

    [Fact]
    public async Task An_assignment_cannot_be_created_for_a_course_subject_with_no_teacher()
    {
        using var world = new TestWorld().AsAdmin();

        var error = await Assert.ThrowsAsync<ConflictException>(() => world.Assignments()
            .CreateAsync(NewAssignment(world.UnassignedCourseSubjectId, TestWorld.Start.AddDays(5))));

        Assert.Contains("No teacher is assigned", error.Message);
    }

    [Fact]
    public async Task An_assignment_cannot_be_created_for_an_inactive_course()
    {
        using var world = new TestWorld().AsAdmin();

        var course = world.Db.Courses.Single(c => c.Id == world.MathsCourseId);
        course.IsActive = false;
        await world.Db.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<ConflictException>(() => world.Assignments()
            .CreateAsync(NewAssignment(world.MathsCourseSubjectId, TestWorld.Start.AddDays(5))));

        Assert.Contains("inactive", error.Message);
    }

    [Fact]
    public async Task Publishing_a_draft_reveals_it_to_the_enrolled_students()
    {
        using var world = new TestWorld().AsTeacherA();

        await world.Assignments().PublishAsync(world.DraftAssignmentId);

        world.AsStudentA();
        var visible = await world.Assignments().GetAsync(world.DraftAssignmentId);

        Assert.Equal(AssignmentStatus.Published, visible.Status);
    }

    [Fact]
    public async Task Publishing_is_refused_once_the_deadline_has_passed()
    {
        using var world = new TestWorld().AsTeacherA();

        // The draft's deadline is Start + 7 days; step past it.
        world.Clock.Advance(TimeSpan.FromDays(8));

        await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => world.Assignments().PublishAsync(world.DraftAssignmentId));
    }

    [Fact]
    public async Task Unpublishing_is_refused_once_a_student_has_submitted()
    {
        using var world = new TestWorld();
        world.GiveSubmission(world.OpenAssignmentId, world.StudentAId);
        world.AsTeacherA();

        var error = await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => world.Assignments().RevertToDraftAsync(world.OpenAssignmentId));

        Assert.Contains("already has submissions", error.Message);
    }

    [Fact]
    public async Task Unpublishing_works_while_no_one_has_submitted()
    {
        using var world = new TestWorld().AsTeacherA();

        var reverted = await world.Assignments().RevertToDraftAsync(world.OpenAssignmentId);

        Assert.Equal(AssignmentStatus.Draft, reverted.Status);
    }

    [Fact]
    public async Task Closing_an_assignment_stops_further_submissions()
    {
        using var world = new TestWorld().AsTeacherA();

        await world.Assignments().CloseAsync(world.OpenAssignmentId);

        Assert.False(world.Assignment(world.OpenAssignmentId).AcceptsSubmissionsAt(world.Clock.UtcNow));
    }

    [Fact]
    public async Task Deleting_an_assignment_that_has_submissions_is_refused()
    {
        using var world = new TestWorld();
        world.GiveSubmission(world.OpenAssignmentId, world.StudentAId);
        world.AsTeacherA();

        await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => world.Assignments().DeleteAsync(world.OpenAssignmentId));

        Assert.True(world.Db.Assignments.Any(a => a.Id == world.OpenAssignmentId));
    }

    [Fact]
    public async Task Deleting_an_untouched_assignment_removes_it()
    {
        using var world = new TestWorld().AsTeacherA();

        await world.Assignments().DeleteAsync(world.DraftAssignmentId);

        Assert.False(world.Db.Assignments.Any(a => a.Id == world.DraftAssignmentId));
    }

    [Fact]
    public async Task Maximum_marks_cannot_be_lowered_after_a_submission_has_been_graded()
    {
        using var world = new TestWorld();
        world.GiveSubmission(world.OpenAssignmentId, world.StudentAId, SubmissionStatus.Graded, marks: 18m);
        world.AsTeacherA();

        var request = new UpdateAssignmentRequest(
            "Open worksheet", "Description.", 10m, TestWorld.Start.AddDays(7), false, true);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => world.Assignments().UpdateAsync(world.OpenAssignmentId, request));
    }

    // ---------------------------------------------------------------- filters

    [Fact]
    public async Task Students_can_filter_down_to_work_they_have_not_handed_in()
    {
        using var world = new TestWorld();
        world.GiveSubmission(world.OpenAssignmentId, world.StudentAId);
        world.AsStudentA();

        var pending = await world.Assignments()
            .ListAsync(new AssignmentListQuery { OnlyPending = true, PageSize = 100 });

        Assert.DoesNotContain(pending.Items, a => a.Id == world.OpenAssignmentId);
        Assert.Contains(pending.Items, a => a.Id == world.LateAllowedAssignmentId);
    }

    [Fact]
    public async Task Filtering_by_course_narrows_the_list()
    {
        using var world = new TestWorld().AsAdmin();

        var result = await world.Assignments()
            .ListAsync(new AssignmentListQuery { CourseId = world.PhysicsCourseId, PageSize = 100 });

        Assert.Single(result.Items);
        Assert.Equal(world.OtherTeacherAssignmentId, result.Items[0].Id);
    }

    [Fact]
    public async Task Paging_reports_the_totals_the_client_needs()
    {
        using var world = new TestWorld().AsAdmin();

        var page = await world.Assignments().ListAsync(new AssignmentListQuery { Page = 1, PageSize = 2 });

        Assert.Equal(2, page.Items.Count);
        Assert.Equal(6, page.TotalCount);
        Assert.Equal(3, page.TotalPages);
        Assert.True(page.HasNextPage);
        Assert.False(page.HasPreviousPage);
    }
}
