using AssignmentSystem.Application.Common.Exceptions;
using AssignmentSystem.Application.Features.Academics;
using AssignmentSystem.Application.Features.Assignments;
using AssignmentSystem.Application.Features.Users;
using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Tests.TestSupport;

namespace AssignmentSystem.Tests.Services;

/// <summary>Administrative rules around accounts, courses and enrollment.</summary>
public class UserAndAcademicRuleTests
{
    // ----------------------------------------------------------------- users

    [Fact]
    public async Task An_email_is_normalised_and_must_be_unique()
    {
        using var world = new TestWorld().AsAdmin();
        var users = world.Users();

        var created = await users.CreateAsync(
            new CreateUserRequest("New Teacher", "  New.Teacher@Test.edu ", "Password1", UserRole.Teacher));

        Assert.Equal("new.teacher@test.edu", created.Email);

        var error = await Assert.ThrowsAsync<ConflictException>(() => users.CreateAsync(
            new CreateUserRequest("Impostor", "NEW.TEACHER@test.edu", "Password1", UserRole.Teacher)));

        Assert.Contains("already exists", error.Message);
    }

    [Fact]
    public async Task A_new_account_password_is_stored_hashed()
    {
        using var world = new TestWorld().AsAdmin();

        var created = await world.Users().CreateAsync(
            new CreateUserRequest("New Student", "new.student@test.edu", "Password1", UserRole.Student));

        var stored = world.Db.Users.Single(u => u.Id == created.Id).PasswordHash;

        Assert.DoesNotContain("Password1", stored);
        Assert.StartsWith("100000.", stored);
    }

    [Fact]
    public async Task An_admin_cannot_deactivate_their_own_account()
    {
        using var world = new TestWorld().AsAdmin();

        var error = await Assert.ThrowsAsync<ConflictException>(
            () => world.Users().DeactivateAsync(world.AdminId));

        Assert.Contains("your own account", error.Message);
    }

    [Fact]
    public async Task The_last_active_administrator_cannot_be_deactivated()
    {
        using var world = new TestWorld();

        // Sign in as a second admin so the self-deactivation guard is not what fires.
        var secondAdmin = await world.AsAdmin().Users().CreateAsync(
            new CreateUserRequest("Second Admin", "second.admin@test.edu", "Password1", UserRole.Admin));

        world.CurrentUser.SignInAs(secondAdmin.Id, UserRole.Admin);
        await world.Users().DeactivateAsync(world.AdminId);

        // Only the signed-in admin is left, and they cannot remove themselves either.
        var error = await Assert.ThrowsAsync<ConflictException>(
            () => world.Users().DeactivateAsync(secondAdmin.Id));

        Assert.Contains("your own account", error.Message);
    }

    [Fact]
    public async Task Deactivating_an_account_keeps_the_row_so_history_survives()
    {
        using var world = new TestWorld().AsAdmin();

        await world.Users().DeactivateAsync(world.StudentAId);

        var student = world.Db.Users.Single(u => u.Id == world.StudentAId);
        Assert.False(student.IsActive);
    }

    // --------------------------------------------------------------- courses

    [Fact]
    public async Task A_course_code_is_upper_cased_and_must_be_unique()
    {
        using var world = new TestWorld().AsAdmin();
        var courses = world.Courses();

        var created = await courses.CreateAsync(new CourseRequest("Grade 12", " g12-a ", "2026", true));

        Assert.Equal("G12-A", created.Code);

        await Assert.ThrowsAsync<ConflictException>(
            () => courses.CreateAsync(new CourseRequest("Duplicate", "g12-a", "2026", true)));
    }

    [Fact]
    public async Task A_course_with_assignments_cannot_be_deleted()
    {
        using var world = new TestWorld().AsAdmin();

        var error = await Assert.ThrowsAsync<ConflictException>(
            () => world.Courses().DeleteAsync(world.MathsCourseId));

        Assert.Contains("Deactivate it instead", error.Message);
    }

    [Fact]
    public async Task A_subject_that_is_taught_somewhere_cannot_be_deleted()
    {
        using var world = new TestWorld().AsAdmin();

        await Assert.ThrowsAsync<ConflictException>(
            () => world.Subjects().DeleteAsync(world.MathsSubjectId));
    }

    [Fact]
    public async Task Only_active_teachers_can_be_put_in_charge_of_a_course_subject()
    {
        using var world = new TestWorld().AsAdmin();

        var error = await Assert.ThrowsAsync<ValidationException>(() => world.Courses()
            .AssignTeacherAsync(world.UnassignedCourseSubjectId, new AssignTeacherRequest(world.StudentAId)));

        Assert.Contains("TeacherId", error.Errors.Keys);
    }

    [Fact]
    public async Task A_course_subject_with_assignments_cannot_be_left_without_a_teacher()
    {
        using var world = new TestWorld().AsAdmin();

        var error = await Assert.ThrowsAsync<ConflictException>(() => world.Courses()
            .AssignTeacherAsync(world.MathsCourseSubjectId, new AssignTeacherRequest(null)));

        Assert.Contains("without a teacher", error.Message);
    }

    [Fact]
    public async Task Assigning_a_new_teacher_transfers_who_may_manage_the_assignments()
    {
        using var world = new TestWorld().AsAdmin();

        await world.Courses().AssignTeacherAsync(
            world.MathsCourseSubjectId, new AssignTeacherRequest(world.TeacherBId));

        world.AsTeacherA();
        await Assert.ThrowsAsync<NotFoundException>(
            () => world.Assignments().GetAsync(world.OpenAssignmentId));

        world.AsTeacherB();
        var nowVisible = await world.Assignments().GetAsync(world.OpenAssignmentId);
        Assert.Equal(world.OpenAssignmentId, nowVisible.Id);
    }

    // ----------------------------------------------------------- enrollments

    [Fact]
    public async Task Enrolling_a_student_who_is_already_enrolled_is_a_no_op()
    {
        using var world = new TestWorld().AsAdmin();

        var result = await world.Courses().EnrollStudentsAsync(
            world.MathsCourseId, new EnrollStudentsRequest([world.StudentAId, world.StudentCId]));

        Assert.Equal(3, result.Count);
        Assert.Equal(1, world.Db.Enrollments.Count(e =>
            e.CourseId == world.MathsCourseId && e.StudentId == world.StudentAId));
    }

    [Fact]
    public async Task Only_students_can_be_enrolled()
    {
        using var world = new TestWorld().AsAdmin();

        await Assert.ThrowsAsync<ValidationException>(() => world.Courses()
            .EnrollStudentsAsync(world.MathsCourseId, new EnrollStudentsRequest([world.TeacherBId])));
    }

    [Fact]
    public async Task A_student_with_submissions_cannot_be_removed_from_a_course()
    {
        using var world = new TestWorld();
        world.GiveSubmission(world.OpenAssignmentId, world.StudentAId);
        world.AsAdmin();

        var error = await Assert.ThrowsAsync<ConflictException>(() => world.Courses()
            .RemoveEnrollmentAsync(world.MathsCourseId, world.StudentAId));

        Assert.Contains("has submissions", error.Message);
    }

    [Fact]
    public async Task Removing_a_student_takes_the_courses_assignments_out_of_their_view()
    {
        using var world = new TestWorld().AsAdmin();

        await world.Courses().RemoveEnrollmentAsync(world.MathsCourseId, world.StudentBId);

        world.AsStudentB();
        var visible = await world.Assignments().ListAsync(new AssignmentListQuery());

        Assert.Empty(visible.Items);
    }

    // ------------------------------------------------------ teachable lookup

    [Fact]
    public async Task A_teacher_is_only_offered_their_own_course_subjects()
    {
        using var world = new TestWorld().AsTeacherA();

        var teachable = await world.Courses().ListTeachableAsync();

        Assert.Single(teachable);
        Assert.Equal(world.MathsCourseSubjectId, teachable[0].Id);
    }

    [Fact]
    public async Task An_admin_is_offered_every_course_subject()
    {
        using var world = new TestWorld().AsAdmin();

        var teachable = await world.Courses().ListTeachableAsync();

        Assert.Equal(3, teachable.Count);
    }

    [Fact]
    public async Task A_student_cannot_list_teachable_course_subjects()
    {
        using var world = new TestWorld().AsStudentA();

        await Assert.ThrowsAsync<ForbiddenException>(() => world.Courses().ListTeachableAsync());
    }
}
