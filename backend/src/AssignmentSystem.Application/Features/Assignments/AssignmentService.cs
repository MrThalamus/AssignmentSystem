using AssignmentSystem.Application.Common.Exceptions;
using AssignmentSystem.Application.Common.Interfaces;
using AssignmentSystem.Application.Common.Models;
using AssignmentSystem.Domain.Entities;
using AssignmentSystem.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AssignmentSystem.Application.Features.Assignments;

public interface IAssignmentService
{
    Task<PagedResult<AssignmentDto>> ListAsync(AssignmentListQuery query, CancellationToken ct = default);
    Task<AssignmentDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<AssignmentDto> CreateAsync(CreateAssignmentRequest request, CancellationToken ct = default);
    Task<AssignmentDto> UpdateAsync(Guid id, UpdateAssignmentRequest request, CancellationToken ct = default);
    Task<AssignmentDto> PublishAsync(Guid id, CancellationToken ct = default);
    Task<AssignmentDto> RevertToDraftAsync(Guid id, CancellationToken ct = default);
    Task<AssignmentDto> CloseAsync(Guid id, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

/// <summary>
/// Assignment reads and writes, scoped to what the caller is allowed to see.
/// Every query starts from <see cref="ApplyRoleScope"/> rather than filtering after
/// the fact, so a missing check cannot leak another teacher's or class's data.
/// </summary>
public class AssignmentService : IAssignmentService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _clock;

    public AssignmentService(IApplicationDbContext db, ICurrentUser currentUser, IDateTimeProvider clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    // ----------------------------------------------------------------- reads

    public async Task<PagedResult<AssignmentDto>> ListAsync(
        AssignmentListQuery query, CancellationToken ct = default)
    {
        var assignments = ApplyRoleScope(_db.Assignments.AsNoTracking());

        if (query.CourseId is { } courseId)
            assignments = assignments.Where(a => a.CourseSubject.CourseId == courseId);

        if (query.SubjectId is { } subjectId)
            assignments = assignments.Where(a => a.CourseSubject.SubjectId == subjectId);

        if (query.Status is { } status)
            assignments = assignments.Where(a => a.Status == status);

        if (query.DueBefore is { } dueBefore)
            assignments = assignments.Where(a => a.Deadline <= dueBefore);

        if (query.DueAfter is { } dueAfter)
            assignments = assignments.Where(a => a.Deadline >= dueAfter);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant();
            assignments = assignments.Where(a => a.Title.ToLower().Contains(term));
        }

        var studentId = _currentUser.Role == UserRole.Student ? _currentUser.UserId : null;

        if (query.OnlyPending == true && studentId is { } pendingStudentId)
            assignments = assignments.Where(a => !a.Submissions.Any(s => s.StudentId == pendingStudentId));

        return await Project(assignments.OrderBy(a => a.Deadline), studentId)
            .ToPagedResultAsync(query, ct);
    }

    public async Task<AssignmentDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var studentId = _currentUser.Role == UserRole.Student ? _currentUser.UserId : null;

        return await Project(ApplyRoleScope(_db.Assignments.AsNoTracking()).Where(a => a.Id == id), studentId)
                   .FirstOrDefaultAsync(ct)
               ?? throw NotFoundException.For("Assignment", id);
    }

    // ---------------------------------------------------------------- writes

    public async Task<AssignmentDto> CreateAsync(
        CreateAssignmentRequest request, CancellationToken ct = default)
    {
        var courseSubject = await _db.CourseSubjects
                                .Include(cs => cs.Course)
                                .FirstOrDefaultAsync(cs => cs.Id == request.CourseSubjectId, ct)
                            ?? throw NotFoundException.For("Course subject", request.CourseSubjectId);

        EnsureCanManage(courseSubject.TeacherId);

        if (courseSubject.TeacherId is not { } teacherId)
            throw new ConflictException(
                "No teacher is assigned to this course subject yet, so assignments cannot be created for it.");

        if (!courseSubject.Course.IsActive)
            throw new ConflictException("The course is inactive and cannot receive new assignments.");

        var now = _clock.UtcNow;

        var assignment = new Assignment
        {
            CourseSubjectId = courseSubject.Id,
            CreatedByTeacherId = teacherId,
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            MaxMarks = request.MaxMarks,
            Deadline = ToUtc(request.Deadline),
            AllowLateSubmission = request.AllowLateSubmission,
            AllowResubmission = request.AllowResubmission,
            Status = AssignmentStatus.Draft,
            CreatedAt = now
        };

        if (request.PublishNow)
            assignment.Publish(now);

        _db.Assignments.Add(assignment);
        await _db.SaveChangesAsync(ct);

        return await GetAsync(assignment.Id, ct);
    }

    public async Task<AssignmentDto> UpdateAsync(
        Guid id, UpdateAssignmentRequest request, CancellationToken ct = default)
    {
        var assignment = await LoadForManagementAsync(id, ct);

        var hasGraded = await _db.Submissions
            .AnyAsync(s => s.AssignmentId == id && s.Status == SubmissionStatus.Graded, ct);

        assignment.UpdateDetails(
            request.Title.Trim(),
            request.Description.Trim(),
            request.MaxMarks,
            ToUtc(request.Deadline),
            request.AllowLateSubmission,
            request.AllowResubmission,
            hasGraded,
            _clock.UtcNow);

        await _db.SaveChangesAsync(ct);

        return await GetAsync(id, ct);
    }

    public async Task<AssignmentDto> PublishAsync(Guid id, CancellationToken ct = default)
    {
        var assignment = await LoadForManagementAsync(id, ct);

        assignment.Publish(_clock.UtcNow);
        await _db.SaveChangesAsync(ct);

        return await GetAsync(id, ct);
    }

    public async Task<AssignmentDto> RevertToDraftAsync(Guid id, CancellationToken ct = default)
    {
        var assignment = await LoadForManagementAsync(id, ct);

        var submissionCount = await _db.Submissions.CountAsync(s => s.AssignmentId == id, ct);

        assignment.RevertToDraft(submissionCount, _clock.UtcNow);
        await _db.SaveChangesAsync(ct);

        return await GetAsync(id, ct);
    }

    public async Task<AssignmentDto> CloseAsync(Guid id, CancellationToken ct = default)
    {
        var assignment = await LoadForManagementAsync(id, ct);

        assignment.Close(_clock.UtcNow);
        await _db.SaveChangesAsync(ct);

        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var assignment = await LoadForManagementAsync(id, ct);

        var submissionCount = await _db.Submissions.CountAsync(s => s.AssignmentId == id, ct);
        assignment.EnsureCanBeDeleted(submissionCount);

        _db.Assignments.Remove(assignment);
        await _db.SaveChangesAsync(ct);
    }

    // --------------------------------------------------------------- scoping

    /// <summary>
    /// Narrows the assignment set to what the caller is entitled to read.
    /// Admins see everything, teachers see the course subjects assigned to them, and
    /// students see published or closed work for the courses they are enrolled in -
    /// drafts are invisible to them entirely.
    /// </summary>
    private IQueryable<Assignment> ApplyRoleScope(IQueryable<Assignment> query)
    {
        switch (_currentUser.Role)
        {
            case UserRole.Admin:
                return query;

            case UserRole.Teacher:
                var teacherId = _currentUser.RequireUserId();
                return query.Where(a => a.CourseSubject.TeacherId == teacherId);

            case UserRole.Student:
                var studentId = _currentUser.RequireUserId();
                return query.Where(a =>
                    (a.Status == AssignmentStatus.Published || a.Status == AssignmentStatus.Closed)
                    && a.CourseSubject.Course.Enrollments.Any(e => e.StudentId == studentId));

            default:
                throw new ForbiddenException("You are not allowed to view assignments.");
        }
    }

    /// <summary>
    /// Loads an assignment for a write operation, refusing callers who do not own it.
    /// Kept separate from <see cref="ApplyRoleScope"/> because writes need a tracked
    /// entity and should report 403 rather than 404 for somebody else's assignment.
    /// </summary>
    private async Task<Assignment> LoadForManagementAsync(Guid id, CancellationToken ct)
    {
        var assignment = await _db.Assignments
                             .Include(a => a.CourseSubject)
                             .FirstOrDefaultAsync(a => a.Id == id, ct)
                         ?? throw NotFoundException.For("Assignment", id);

        EnsureCanManage(assignment.CourseSubject.TeacherId);

        return assignment;
    }

    private void EnsureCanManage(Guid? owningTeacherId)
    {
        switch (_currentUser.Role)
        {
            case UserRole.Admin:
                return;

            case UserRole.Teacher when owningTeacherId == _currentUser.RequireUserId():
                return;

            case UserRole.Teacher:
                throw new ForbiddenException("You can only manage assignments for your own course subjects.");

            default:
                throw new ForbiddenException("Only teachers and administrators can manage assignments.");
        }
    }

    // -------------------------------------------------------------- helpers

    /// <summary>
    /// Deadlines arrive as ISO-8601 from the client. Normalising to UTC here keeps
    /// PostgreSQL's <c>timestamptz</c> columns happy and makes deadline comparisons
    /// independent of the caller's time zone.
    /// </summary>
    private static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private static IQueryable<AssignmentDto> Project(IQueryable<Assignment> query, Guid? studentId) =>
        query.Select(a => new AssignmentDto(
            a.Id,
            a.Title,
            a.Description,
            a.MaxMarks,
            a.Deadline,
            a.Status,
            a.AllowLateSubmission,
            a.AllowResubmission,
            a.CreatedAt,
            a.PublishedAt,
            a.CourseSubjectId,
            a.CourseSubject.CourseId,
            a.CourseSubject.Course.Name,
            a.CourseSubject.Course.Code,
            a.CourseSubject.SubjectId,
            a.CourseSubject.Subject.Name,
            a.CreatedByTeacherId,
            a.CreatedByTeacher.FullName,
            // Class-wide counts are staff information; a student sees only their own row.
            studentId == null ? a.Submissions.Count : 0,
            studentId == null ? a.Submissions.Count(s => s.Status == SubmissionStatus.Graded) : 0,
            studentId == null
                ? null
                : a.Submissions
                    .Where(s => s.StudentId == studentId)
                    .Select(s => new StudentSubmissionSummary(s.Id, s.Status, s.IsLate, s.SubmittedAt, s.Marks))
                    .FirstOrDefault()));
}
