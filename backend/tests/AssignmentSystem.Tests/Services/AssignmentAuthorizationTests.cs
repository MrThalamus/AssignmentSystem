using AssignmentSystem.Application.Common.Exceptions;
using AssignmentSystem.Application.Features.Assignments;
using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Tests.TestSupport;

namespace AssignmentSystem.Tests.Services;

/// <summary>
/// Who can see and change which assignment. These are the checks that stop one
/// teacher from touching another's work and stop a student from seeing a draft or a
/// class they are not in.
/// </summary>
public class AssignmentAuthorizationTests
{
    // -------------------------------------------------------------- visibility

    [Fact]
    public async Task An_admin_sees_every_assignment_including_drafts()
    {
        using var world = new TestWorld().AsAdmin();

        var result = await world.Assignments().ListAsync(new AssignmentListQuery { PageSize = 100 });

        Assert.Equal(6, result.TotalCount);
        Assert.Contains(result.Items, a => a.Status == AssignmentStatus.Draft);
    }

    [Fact]
    public async Task A_teacher_sees_only_the_assignments_for_their_own_course_subjects()
    {
        using var world = new TestWorld().AsTeacherA();

        var result = await world.Assignments().ListAsync(new AssignmentListQuery { PageSize = 100 });

        Assert.Equal(5, result.TotalCount);
        Assert.DoesNotContain(result.Items, a => a.Id == world.OtherTeacherAssignmentId);
    }

    [Fact]
    public async Task A_teacher_sees_their_own_drafts()
    {
        using var world = new TestWorld().AsTeacherA();

        var result = await world.Assignments().ListAsync(new AssignmentListQuery { PageSize = 100 });

        Assert.Contains(result.Items, a => a.Id == world.DraftAssignmentId);
    }

    [Fact]
    public async Task A_teacher_cannot_open_another_teachers_assignment()
    {
        using var world = new TestWorld().AsTeacherA();

        await Assert.ThrowsAsync<NotFoundException>(
            () => world.Assignments().GetAsync(world.OtherTeacherAssignmentId));
    }

    [Fact]
    public async Task A_student_never_sees_a_draft()
    {
        using var world = new TestWorld().AsStudentA();

        var result = await world.Assignments().ListAsync(new AssignmentListQuery { PageSize = 100 });

        Assert.DoesNotContain(result.Items, a => a.Status == AssignmentStatus.Draft);
        await Assert.ThrowsAsync<NotFoundException>(
            () => world.Assignments().GetAsync(world.DraftAssignmentId));
    }

    [Fact]
    public async Task A_student_sees_only_the_assignments_for_courses_they_are_enrolled_in()
    {
        using var world = new TestWorld().AsStudentA();

        var result = await world.Assignments().ListAsync(new AssignmentListQuery { PageSize = 100 });

        // The four published-or-closed maths assignments, and nothing from physics.
        Assert.Equal(4, result.TotalCount);
        Assert.DoesNotContain(result.Items, a => a.Id == world.OtherTeacherAssignmentId);
    }

    [Fact]
    public async Task A_student_in_another_course_sees_that_courses_work_instead()
    {
        using var world = new TestWorld().AsStudentC();

        var result = await world.Assignments().ListAsync(new AssignmentListQuery { PageSize = 100 });

        Assert.Single(result.Items);
        Assert.Equal(world.OtherTeacherAssignmentId, result.Items[0].Id);
    }

    [Fact]
    public async Task A_student_still_sees_a_closed_assignment()
    {
        using var world = new TestWorld().AsStudentA();

        var assignment = await world.Assignments().GetAsync(world.ClosedAssignmentId);

        Assert.Equal(AssignmentStatus.Closed, assignment.Status);
    }

    [Fact]
    public async Task A_student_is_not_shown_how_many_classmates_have_submitted()
    {
        using var world = new TestWorld();
        world.GiveSubmission(world.OpenAssignmentId, world.StudentBId);
        world.AsStudentA();

        var assignment = await world.Assignments().GetAsync(world.OpenAssignmentId);

        Assert.Equal(0, assignment.SubmissionCount);
        Assert.Null(assignment.MySubmission);
    }

    [Fact]
    public async Task A_student_sees_their_own_submission_summary_on_the_assignment()
    {
        using var world = new TestWorld();
        world.GiveSubmission(world.OpenAssignmentId, world.StudentAId, SubmissionStatus.Graded, marks: 16m);
        world.AsStudentA();

        var assignment = await world.Assignments().GetAsync(world.OpenAssignmentId);

        Assert.NotNull(assignment.MySubmission);
        Assert.Equal(16m, assignment.MySubmission!.Marks);
    }

    // ----------------------------------------------------------- management

    [Fact]
    public async Task A_teacher_cannot_edit_another_teachers_assignment()
    {
        using var world = new TestWorld().AsTeacherA();

        var request = new UpdateAssignmentRequest(
            "Hijacked", "Changed by the wrong teacher.", 20m, TestWorld.Start.AddDays(5), false, true);

        await Assert.ThrowsAsync<ForbiddenException>(
            () => world.Assignments().UpdateAsync(world.OtherTeacherAssignmentId, request));
    }

    [Fact]
    public async Task A_teacher_cannot_delete_another_teachers_assignment()
    {
        using var world = new TestWorld().AsTeacherA();

        await Assert.ThrowsAsync<ForbiddenException>(
            () => world.Assignments().DeleteAsync(world.OtherTeacherAssignmentId));
    }

    [Fact]
    public async Task A_teacher_cannot_publish_another_teachers_assignment()
    {
        using var world = new TestWorld().AsTeacherB();

        await Assert.ThrowsAsync<ForbiddenException>(
            () => world.Assignments().PublishAsync(world.DraftAssignmentId));
    }

    [Fact]
    public async Task A_teacher_cannot_create_an_assignment_for_a_course_subject_they_do_not_teach()
    {
        using var world = new TestWorld().AsTeacherA();

        var request = new CreateAssignmentRequest(
            world.PhysicsCourseSubjectId, "Not mine", "Description.", 10m,
            TestWorld.Start.AddDays(5), false, true, PublishNow: false);

        await Assert.ThrowsAsync<ForbiddenException>(() => world.Assignments().CreateAsync(request));
    }

    [Fact]
    public async Task A_student_cannot_create_an_assignment()
    {
        using var world = new TestWorld().AsStudentA();

        var request = new CreateAssignmentRequest(
            world.MathsCourseSubjectId, "Homework for the teacher", "Description.", 10m,
            TestWorld.Start.AddDays(5), false, true, PublishNow: false);

        await Assert.ThrowsAsync<ForbiddenException>(() => world.Assignments().CreateAsync(request));
    }

    [Fact]
    public async Task An_admin_may_manage_any_teachers_assignment()
    {
        using var world = new TestWorld().AsAdmin();

        var request = new UpdateAssignmentRequest(
            "Corrected title", "Fixed by the administrator.", 20m,
            TestWorld.Start.AddDays(5), false, true);

        var updated = await world.Assignments().UpdateAsync(world.OtherTeacherAssignmentId, request);

        Assert.Equal("Corrected title", updated.Title);
    }
}
