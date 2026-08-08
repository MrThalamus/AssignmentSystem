using AssignmentSystem.Application.Common.Exceptions;
using AssignmentSystem.Application.Features.Submissions;
using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Domain.Exceptions;
using AssignmentSystem.Tests.TestSupport;

namespace AssignmentSystem.Tests.Services;

/// <summary>
/// The end-to-end submission workflow through the service: handing in, revising,
/// grading, returning for revision, and the scoping that keeps students out of each
/// other's work.
/// </summary>
public class SubmissionWorkflowTests
{
    // ------------------------------------------------------------ handing in

    [Fact]
    public async Task A_student_can_submit_to_a_published_assignment_for_their_course()
    {
        using var world = new TestWorld().AsStudentA();

        var submission = await world.Submissions().SubmitAsync(
            new CreateSubmissionRequest(world.OpenAssignmentId, TestPdf.Upload("my-answer")));

        Assert.Equal(SubmissionStatus.Submitted, submission.Status);
        Assert.Equal("my-answer.pdf", submission.FileName);
        Assert.Equal(world.StudentAId, submission.StudentId);
        Assert.False(submission.IsLate);
    }

    [Fact]
    public async Task A_student_cannot_submit_to_a_draft_assignment()
    {
        using var world = new TestWorld().AsStudentA();

        // Reported as "not found" rather than "forbidden": a draft must not be
        // discoverable by the students it is not yet meant for.
        await Assert.ThrowsAsync<NotFoundException>(() => world.Submissions()
            .SubmitAsync(new CreateSubmissionRequest(world.DraftAssignmentId, TestPdf.Upload("too-early"))));
    }

    [Fact]
    public async Task A_student_cannot_submit_to_a_course_they_are_not_enrolled_in()
    {
        using var world = new TestWorld().AsStudentA();

        await Assert.ThrowsAsync<NotFoundException>(() => world.Submissions()
            .SubmitAsync(new CreateSubmissionRequest(world.OtherTeacherAssignmentId, TestPdf.Upload("not-my-class"))));
    }

    [Fact]
    public async Task A_teacher_cannot_submit_an_answer()
    {
        using var world = new TestWorld().AsTeacherA();

        await Assert.ThrowsAsync<ForbiddenException>(() => world.Submissions()
            .SubmitAsync(new CreateSubmissionRequest(world.OpenAssignmentId, TestPdf.Upload("teacher-s-answer"))));
    }

    [Fact]
    public async Task Submitting_after_the_deadline_is_refused_when_late_work_is_barred()
    {
        using var world = new TestWorld().AsStudentA();
        world.Clock.Advance(TimeSpan.FromDays(8));

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() => world.Submissions()
            .SubmitAsync(new CreateSubmissionRequest(world.OpenAssignmentId, TestPdf.Upload("too-late"))));
    }

    [Fact]
    public async Task Submitting_after_the_deadline_is_marked_late_when_the_assignment_allows_it()
    {
        using var world = new TestWorld().AsStudentA();
        world.Clock.Advance(TimeSpan.FromDays(3));

        var submission = await world.Submissions().SubmitAsync(
            new CreateSubmissionRequest(world.LateAllowedAssignmentId, TestPdf.Upload("better-late-than-never")));

        Assert.Equal(SubmissionStatus.Late, submission.Status);
        Assert.True(submission.IsLate);
    }

    [Fact]
    public async Task Submitting_twice_revises_the_existing_submission_instead_of_creating_a_second()
    {
        using var world = new TestWorld().AsStudentA();
        var service = world.Submissions();

        var first = await service.SubmitAsync(
            new CreateSubmissionRequest(world.OpenAssignmentId, TestPdf.Upload("first-attempt")));

        world.Clock.Advance(TimeSpan.FromHours(2));

        var second = await service.SubmitAsync(
            new CreateSubmissionRequest(world.OpenAssignmentId, TestPdf.Upload("second-attempt")));

        Assert.Equal(first.Id, second.Id);
        Assert.Equal("second-attempt.pdf", second.FileName);
        Assert.Equal(2, second.AttemptCount);
        Assert.Equal(1, world.Db.Submissions.Count(s =>
            s.AssignmentId == world.OpenAssignmentId && s.StudentId == world.StudentAId));
    }

    [Fact]
    public async Task Revising_is_refused_when_the_assignment_permits_only_one_attempt()
    {
        using var world = new TestWorld().AsStudentA();
        var service = world.Submissions();

        await service.SubmitAsync(new CreateSubmissionRequest(world.NoResubmitAssignmentId, TestPdf.Upload("final-answer")));

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() => service
            .SubmitAsync(new CreateSubmissionRequest(world.NoResubmitAssignmentId, TestPdf.Upload("changed-my-mind"))));
    }

    [Fact]
    public async Task A_student_cannot_update_another_students_submission()
    {
        using var world = new TestWorld();
        var theirs = world.GiveSubmission(world.OpenAssignmentId, world.StudentBId);
        world.AsStudentA();

        await Assert.ThrowsAsync<ForbiddenException>(() => world.Submissions()
            .UpdateAsync(theirs.Id, new UpdateSubmissionRequest(TestPdf.Upload("tampered"))));
    }

    // ----------------------------------------------------------------- files

    [Fact]
    public async Task Handing_in_stores_the_pdf_and_records_its_name_type_and_size()
    {
        using var world = new TestWorld().AsStudentA();
        var upload = TestPdf.Upload("essay");

        var submission = await world.Submissions().SubmitAsync(
            new CreateSubmissionRequest(world.OpenAssignmentId, upload));

        Assert.Equal("essay.pdf", submission.FileName);
        Assert.Equal("application/pdf", submission.ContentType);
        Assert.Equal(upload.Content.Length, submission.FileSizeBytes);
        Assert.Equal(upload.Content, world.Db.SubmissionFiles.Single().Content);
    }

    [Fact]
    public async Task A_file_that_is_not_a_pdf_is_refused_however_it_is_named()
    {
        using var world = new TestWorld().AsStudentA();

        // Named and declared as a PDF; only the bytes give it away.
        var error = await Assert.ThrowsAsync<ValidationException>(() => world.Submissions()
            .SubmitAsync(new CreateSubmissionRequest(world.OpenAssignmentId, TestPdf.NotAPdf())));

        Assert.Contains("not a valid PDF", error.Errors[SubmissionFileRules.FieldName][0]);
        Assert.Empty(world.Db.Submissions);
    }

    [Fact]
    public async Task Handing_in_without_a_file_is_refused()
    {
        using var world = new TestWorld().AsStudentA();

        await Assert.ThrowsAsync<ValidationException>(() => world.Submissions()
            .SubmitAsync(new CreateSubmissionRequest(world.OpenAssignmentId, null)));
    }

    [Fact]
    public async Task A_file_over_the_size_limit_is_refused()
    {
        using var world = new TestWorld().AsStudentA();

        var oversized = new UploadedFile(
            "huge.pdf", "application/pdf", new byte[SubmissionFileRules.MaxSizeBytes + 1]);

        var error = await Assert.ThrowsAsync<ValidationException>(() => world.Submissions()
            .SubmitAsync(new CreateSubmissionRequest(world.OpenAssignmentId, oversized)));

        Assert.Contains("larger than", error.Errors[SubmissionFileRules.FieldName][0]);
    }

    [Fact]
    public async Task Revising_replaces_the_stored_pdf_rather_than_keeping_both()
    {
        using var world = new TestWorld().AsStudentA();
        var service = world.Submissions();

        await service.SubmitAsync(new CreateSubmissionRequest(world.OpenAssignmentId, TestPdf.Upload("draft")));

        var better = TestPdf.Upload("final");
        await service.SubmitAsync(new CreateSubmissionRequest(world.OpenAssignmentId, better));

        var stored = Assert.Single(world.Db.SubmissionFiles);
        Assert.Equal(better.Content, stored.Content);
    }

    [Fact]
    public async Task A_student_can_download_their_own_pdf()
    {
        using var world = new TestWorld().AsStudentA();
        var upload = TestPdf.Upload("my-work");

        var submission = await world.Submissions().SubmitAsync(
            new CreateSubmissionRequest(world.OpenAssignmentId, upload));

        var file = await world.Submissions().DownloadAsync(submission.Id);

        Assert.Equal("my-work.pdf", file.FileName);
        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal(upload.Content, file.Content);
    }

    [Fact]
    public async Task A_student_cannot_download_another_students_pdf()
    {
        using var world = new TestWorld();
        var theirs = world.GiveSubmission(world.OpenAssignmentId, world.StudentBId);
        world.AsStudentA();

        await Assert.ThrowsAsync<NotFoundException>(() => world.Submissions().DownloadAsync(theirs.Id));
    }

    [Fact]
    public async Task A_teacher_cannot_download_a_pdf_from_another_teachers_assignment()
    {
        using var world = new TestWorld();
        var mine = world.GiveSubmission(world.OpenAssignmentId, world.StudentAId);
        var theirs = world.GiveSubmission(world.OtherTeacherAssignmentId, world.StudentCId);
        world.AsTeacherA();

        Assert.NotEmpty((await world.Submissions().DownloadAsync(mine.Id)).Content);

        await Assert.ThrowsAsync<NotFoundException>(() => world.Submissions().DownloadAsync(theirs.Id));
    }

    // -------------------------------------------------------------- grading

    [Fact]
    public async Task A_teacher_can_grade_a_submission_for_their_own_course_subject()
    {
        using var world = new TestWorld();
        var submission = world.GiveSubmission(world.OpenAssignmentId, world.StudentAId);
        world.AsTeacherA();

        var graded = await world.Submissions()
            .GradeAsync(submission.Id, new GradeSubmissionRequest(17m, "  Solid work.  "));

        Assert.Equal(SubmissionStatus.Graded, graded.Status);
        Assert.Equal(17m, graded.Marks);
        Assert.Equal("Solid work.", graded.Feedback);
        Assert.Equal("Teacher A", graded.GradedByTeacherName);
    }

    [Fact]
    public async Task A_teacher_cannot_grade_a_submission_belonging_to_another_teachers_assignment()
    {
        using var world = new TestWorld();
        var submission = world.GiveSubmission(world.OtherTeacherAssignmentId, world.StudentCId);
        world.AsTeacherA();

        var error = await Assert.ThrowsAsync<ForbiddenException>(() => world.Submissions()
            .GradeAsync(submission.Id, new GradeSubmissionRequest(20m, "Not mine to mark.")));

        Assert.Contains("your own course subjects", error.Message);
    }

    [Fact]
    public async Task A_student_cannot_grade_their_own_submission()
    {
        using var world = new TestWorld();
        var submission = world.GiveSubmission(world.OpenAssignmentId, world.StudentAId);
        world.AsStudentA();

        await Assert.ThrowsAsync<ForbiddenException>(() => world.Submissions()
            .GradeAsync(submission.Id, new GradeSubmissionRequest(20m, "Full marks please.")));
    }

    [Fact]
    public async Task Marks_above_the_assignment_maximum_are_refused()
    {
        using var world = new TestWorld();
        var submission = world.GiveSubmission(world.OpenAssignmentId, world.StudentAId);
        world.AsTeacherA();

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() => world.Submissions()
            .GradeAsync(submission.Id, new GradeSubmissionRequest(21m, null)));
    }

    [Fact]
    public async Task A_graded_submission_can_no_longer_be_edited_by_the_student()
    {
        using var world = new TestWorld();
        var submission = world.GiveSubmission(world.OpenAssignmentId, world.StudentAId);
        world.AsTeacherA();
        await world.Submissions().GradeAsync(submission.Id, new GradeSubmissionRequest(15m, null));

        world.AsStudentA();

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() => world.Submissions()
            .UpdateAsync(submission.Id, new UpdateSubmissionRequest(TestPdf.Upload("let-me-improve-that"))));
    }

    [Fact]
    public async Task Returning_work_lets_the_student_resubmit_after_the_deadline()
    {
        using var world = new TestWorld();
        var submission = world.GiveSubmission(world.OpenAssignmentId, world.StudentAId);

        world.AsTeacherA();
        await world.Submissions().ChangeStatusAsync(
            submission.Id, new ChangeSubmissionStatusRequest(SubmissionStatus.Returned));

        // Well past the deadline, on an assignment that bars late submissions.
        world.Clock.Advance(TimeSpan.FromDays(9));
        world.AsStudentA();

        var revised = await world.Submissions()
            .UpdateAsync(submission.Id, new UpdateSubmissionRequest(TestPdf.Upload("revised-after-feedback")));

        Assert.Equal("revised-after-feedback.pdf", revised.FileName);
    }

    [Fact]
    public async Task Returning_a_graded_submission_clears_its_marks()
    {
        using var world = new TestWorld();
        var submission = world.GiveSubmission(
            world.OpenAssignmentId, world.StudentAId, SubmissionStatus.Graded, marks: 12m);
        world.AsTeacherA();

        var returned = await world.Submissions().ChangeStatusAsync(
            submission.Id, new ChangeSubmissionStatusRequest(SubmissionStatus.Returned));

        Assert.Equal(SubmissionStatus.Returned, returned.Status);
        Assert.Null(returned.Marks);
    }

    // -------------------------------------------------------------- scoping

    [Fact]
    public async Task A_student_sees_only_their_own_submissions()
    {
        using var world = new TestWorld();
        world.GiveSubmission(world.OpenAssignmentId, world.StudentAId);
        world.GiveSubmission(world.OpenAssignmentId, world.StudentBId);
        world.AsStudentA();

        var result = await world.Submissions().ListAsync(new SubmissionListQuery { PageSize = 100 });

        Assert.Single(result.Items);
        Assert.Equal(world.StudentAId, result.Items[0].StudentId);
    }

    [Fact]
    public async Task A_student_cannot_read_another_students_submission_by_id()
    {
        using var world = new TestWorld();
        var theirs = world.GiveSubmission(world.OpenAssignmentId, world.StudentBId);
        world.AsStudentA();

        await Assert.ThrowsAsync<NotFoundException>(() => world.Submissions().GetAsync(theirs.Id));
    }

    [Fact]
    public async Task A_student_cannot_filter_the_list_to_another_student()
    {
        using var world = new TestWorld();
        world.GiveSubmission(world.OpenAssignmentId, world.StudentBId);
        world.AsStudentA();

        await Assert.ThrowsAsync<ForbiddenException>(() => world.Submissions()
            .ListAsync(new SubmissionListQuery { StudentId = world.StudentBId }));
    }

    [Fact]
    public async Task A_teacher_sees_every_submission_for_their_own_assignments()
    {
        using var world = new TestWorld();
        world.GiveSubmission(world.OpenAssignmentId, world.StudentAId);
        world.GiveSubmission(world.OpenAssignmentId, world.StudentBId);
        world.GiveSubmission(world.OtherTeacherAssignmentId, world.StudentCId);
        world.AsTeacherA();

        var result = await world.Submissions().ListAsync(new SubmissionListQuery { PageSize = 100 });

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, s => Assert.Equal(world.OpenAssignmentId, s.AssignmentId));
    }

    [Fact]
    public async Task An_admin_sees_submissions_across_every_teacher()
    {
        using var world = new TestWorld();
        world.GiveSubmission(world.OpenAssignmentId, world.StudentAId);
        world.GiveSubmission(world.OtherTeacherAssignmentId, world.StudentCId);
        world.AsAdmin();

        var result = await world.Submissions().ListAsync(new SubmissionListQuery { PageSize = 100 });

        Assert.Equal(2, result.TotalCount);
    }
}
