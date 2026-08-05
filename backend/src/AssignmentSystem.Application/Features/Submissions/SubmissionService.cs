using AssignmentSystem.Application.Common.Exceptions;
using AssignmentSystem.Application.Common.Interfaces;
using AssignmentSystem.Application.Common.Models;
using AssignmentSystem.Domain.Entities;
using AssignmentSystem.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AssignmentSystem.Application.Features.Submissions;

public interface ISubmissionService
{
    Task<PagedResult<SubmissionDto>> ListAsync(SubmissionListQuery query, CancellationToken ct = default);
    Task<SubmissionDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<SubmissionDto> SubmitAsync(CreateSubmissionRequest request, CancellationToken ct = default);
    Task<SubmissionDto> UpdateAsync(Guid id, UpdateSubmissionRequest request, CancellationToken ct = default);
    Task<SubmissionDto> GradeAsync(Guid id, GradeSubmissionRequest request, CancellationToken ct = default);
    Task<SubmissionDto> ChangeStatusAsync(Guid id, ChangeSubmissionStatusRequest request, CancellationToken ct = default);
}

/// <summary>
/// The submission workflow: hand in, revise, grade, and move between states.
/// The state rules themselves live on <see cref="Submission"/> and
/// <see cref="Assignment"/>; this class decides who is allowed to invoke them.
/// </summary>
public class SubmissionService : ISubmissionService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _clock;

    public SubmissionService(IApplicationDbContext db, ICurrentUser currentUser, IDateTimeProvider clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    // ----------------------------------------------------------------- reads

    public async Task<PagedResult<SubmissionDto>> ListAsync(
        SubmissionListQuery query, CancellationToken ct = default)
    {
        var submissions = ApplyRoleScope(_db.Submissions.AsNoTracking());

        if (query.AssignmentId is { } assignmentId)
            submissions = submissions.Where(s => s.AssignmentId == assignmentId);

        if (query.CourseId is { } courseId)
            submissions = submissions.Where(s => s.Assignment.CourseSubject.CourseId == courseId);

        if (query.Status is { } status)
            submissions = submissions.Where(s => s.Status == status);

        if (query.StudentId is { } studentId)
        {
            // A student may only ever filter to themselves; anything else is a
            // request for another person's work.
            if (_currentUser.Role == UserRole.Student && studentId != _currentUser.UserId)
                throw new ForbiddenException("You can only view your own submissions.");

            submissions = submissions.Where(s => s.StudentId == studentId);
        }

        return await Project(submissions.OrderByDescending(s => s.SubmittedAt))
            .ToPagedResultAsync(query, ct);
    }

    public async Task<SubmissionDto> GetAsync(Guid id, CancellationToken ct = default) =>
        await Project(ApplyRoleScope(_db.Submissions.AsNoTracking()).Where(s => s.Id == id))
            .FirstOrDefaultAsync(ct)
        ?? throw NotFoundException.For("Submission", id);

    // -------------------------------------------------------- student writes

    public async Task<SubmissionDto> SubmitAsync(
        CreateSubmissionRequest request, CancellationToken ct = default)
    {
        var studentId = RequireStudent();

        var assignment = await _db.Assignments
                             .Include(a => a.CourseSubject)
                             .FirstOrDefaultAsync(a => a.Id == request.AssignmentId, ct)
                         ?? throw NotFoundException.For("Assignment", request.AssignmentId);

        // A draft assignment must stay invisible, so an unenrolled or too-early
        // request is reported as "not found" rather than confirming it exists.
        if (!assignment.IsVisibleToStudents || !await IsEnrolledAsync(studentId, assignment, ct))
            throw NotFoundException.For("Assignment", request.AssignmentId);

        var existing = await _db.Submissions
            .FirstOrDefaultAsync(s => s.AssignmentId == assignment.Id && s.StudentId == studentId, ct);

        // One submission per student per assignment: a second POST revises the first
        // rather than creating a duplicate the teacher would have to reconcile.
        if (existing is not null)
        {
            existing.UpdateAnswer(assignment, request.Content.Trim(), Normalise(request.AttachmentUrl), _clock.UtcNow);
            await _db.SaveChangesAsync(ct);
            return await GetAsync(existing.Id, ct);
        }

        var submission = Submission.Create(
            assignment, studentId, request.Content.Trim(), Normalise(request.AttachmentUrl), _clock.UtcNow);

        _db.Submissions.Add(submission);
        await _db.SaveChangesAsync(ct);

        return await GetAsync(submission.Id, ct);
    }

    public async Task<SubmissionDto> UpdateAsync(
        Guid id, UpdateSubmissionRequest request, CancellationToken ct = default)
    {
        var studentId = RequireStudent();

        var submission = await _db.Submissions
                             .Include(s => s.Assignment)
                             .FirstOrDefaultAsync(s => s.Id == id, ct)
                         ?? throw NotFoundException.For("Submission", id);

        if (submission.StudentId != studentId)
            throw new ForbiddenException("You can only update your own submission.");

        submission.UpdateAnswer(
            submission.Assignment, request.Content.Trim(), Normalise(request.AttachmentUrl), _clock.UtcNow);

        await _db.SaveChangesAsync(ct);

        return await GetAsync(id, ct);
    }

    // -------------------------------------------------------- teacher writes

    public async Task<SubmissionDto> GradeAsync(
        Guid id, GradeSubmissionRequest request, CancellationToken ct = default)
    {
        var submission = await LoadForGradingAsync(id, ct);

        submission.Grade(
            submission.Assignment,
            request.Marks,
            string.IsNullOrWhiteSpace(request.Feedback) ? null : request.Feedback.Trim(),
            _currentUser.RequireUserId(),
            _clock.UtcNow);

        await _db.SaveChangesAsync(ct);

        return await GetAsync(id, ct);
    }

    public async Task<SubmissionDto> ChangeStatusAsync(
        Guid id, ChangeSubmissionStatusRequest request, CancellationToken ct = default)
    {
        var submission = await LoadForGradingAsync(id, ct);

        submission.ChangeStatus(request.Status, _clock.UtcNow);
        await _db.SaveChangesAsync(ct);

        return await GetAsync(id, ct);
    }

    // --------------------------------------------------------------- scoping

    /// <summary>
    /// Students see only their own work, teachers see everything handed in for the
    /// course subjects assigned to them, and admins see all of it.
    /// </summary>
    private IQueryable<Submission> ApplyRoleScope(IQueryable<Submission> query)
    {
        switch (_currentUser.Role)
        {
            case UserRole.Admin:
                return query;

            case UserRole.Teacher:
                var teacherId = _currentUser.RequireUserId();
                return query.Where(s => s.Assignment.CourseSubject.TeacherId == teacherId);

            case UserRole.Student:
                var studentId = _currentUser.RequireUserId();
                return query.Where(s => s.StudentId == studentId);

            default:
                throw new ForbiddenException("You are not allowed to view submissions.");
        }
    }

    /// <summary>Loads a tracked submission, refusing teachers who do not own the assignment.</summary>
    private async Task<Submission> LoadForGradingAsync(Guid id, CancellationToken ct)
    {
        var submission = await _db.Submissions
                             .Include(s => s.Assignment)
                             .ThenInclude(a => a.CourseSubject)
                             .FirstOrDefaultAsync(s => s.Id == id, ct)
                         ?? throw NotFoundException.For("Submission", id);

        switch (_currentUser.Role)
        {
            case UserRole.Admin:
                break;

            case UserRole.Teacher when submission.Assignment.CourseSubject.TeacherId == _currentUser.RequireUserId():
                break;

            case UserRole.Teacher:
                throw new ForbiddenException("You can only grade submissions for your own course subjects.");

            default:
                throw new ForbiddenException("Only teachers and administrators can grade submissions.");
        }

        return submission;
    }

    // -------------------------------------------------------------- helpers

    private Guid RequireStudent()
    {
        if (_currentUser.Role != UserRole.Student)
            throw new ForbiddenException("Only students can submit answers.");

        return _currentUser.RequireUserId();
    }

    private Task<bool> IsEnrolledAsync(Guid studentId, Assignment assignment, CancellationToken ct) =>
        _db.Enrollments.AnyAsync(
            e => e.StudentId == studentId && e.CourseId == assignment.CourseSubject.CourseId, ct);

    private static string? Normalise(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IQueryable<SubmissionDto> Project(IQueryable<Submission> query) =>
        query.Select(s => new SubmissionDto(
            s.Id,
            s.AssignmentId,
            s.Assignment.Title,
            s.Assignment.MaxMarks,
            s.Assignment.Deadline,
            s.StudentId,
            s.Student.FullName,
            s.Student.Email,
            s.Content,
            s.AttachmentUrl,
            s.Status,
            s.IsLate,
            s.AttemptCount,
            s.SubmittedAt,
            s.UpdatedAt,
            s.Marks,
            s.Feedback,
            s.GradedAt,
            s.GradedByTeacher != null ? s.GradedByTeacher.FullName : null));
}
