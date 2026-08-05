using AssignmentSystem.Application.Common.Exceptions;
using AssignmentSystem.Application.Common.Interfaces;
using AssignmentSystem.Application.Common.Models;
using AssignmentSystem.Domain.Entities;
using AssignmentSystem.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AssignmentSystem.Application.Features.Academics;

public interface ICourseService
{
    Task<PagedResult<CourseDto>> ListAsync(CourseListQuery query, CancellationToken ct = default);
    Task<CourseDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<CourseDto> CreateAsync(CourseRequest request, CancellationToken ct = default);
    Task<CourseDto> UpdateAsync(Guid id, CourseRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<CourseSubjectDto>> ListCourseSubjectsAsync(Guid courseId, CancellationToken ct = default);
    Task<CourseSubjectDto> AddSubjectAsync(Guid courseId, AddCourseSubjectRequest request, CancellationToken ct = default);
    Task<CourseSubjectDto> AssignTeacherAsync(Guid courseSubjectId, AssignTeacherRequest request, CancellationToken ct = default);
    Task RemoveSubjectAsync(Guid courseSubjectId, CancellationToken ct = default);

    Task<IReadOnlyList<EnrollmentDto>> ListEnrollmentsAsync(Guid courseId, CancellationToken ct = default);
    Task<IReadOnlyList<EnrollmentDto>> EnrollStudentsAsync(Guid courseId, EnrollStudentsRequest request, CancellationToken ct = default);
    Task RemoveEnrollmentAsync(Guid courseId, Guid studentId, CancellationToken ct = default);

    /// <summary>
    /// The course/subject pairings the caller may create assignments for: everything
    /// for an admin, only their own for a teacher.
    /// </summary>
    Task<IReadOnlyList<CourseSubjectDto>> ListTeachableAsync(CancellationToken ct = default);
}

public class CourseService : ICourseService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _clock;

    public CourseService(IApplicationDbContext db, ICurrentUser currentUser, IDateTimeProvider clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    // -------------------------------------------------------------- courses

    public async Task<PagedResult<CourseDto>> ListAsync(CourseListQuery query, CancellationToken ct = default)
    {
        var courses = _db.Courses.AsNoTracking();

        if (query.IsActive is { } isActive)
            courses = courses.Where(c => c.IsActive == isActive);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant();
            courses = courses.Where(c => c.Name.ToLower().Contains(term) || c.Code.ToLower().Contains(term));
        }

        return await courses
            .OrderBy(c => c.Name)
            .Select(c => new CourseDto(
                c.Id,
                c.Name,
                c.Code,
                c.AcademicYear,
                c.IsActive,
                c.Enrollments.Count,
                c.CourseSubjects.Count))
            .ToPagedResultAsync(query, ct);
    }

    public async Task<CourseDto> GetAsync(Guid id, CancellationToken ct = default) =>
        await _db.Courses
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new CourseDto(
                c.Id, c.Name, c.Code, c.AcademicYear, c.IsActive,
                c.Enrollments.Count, c.CourseSubjects.Count))
            .FirstOrDefaultAsync(ct)
        ?? throw NotFoundException.For("Course", id);

    public async Task<CourseDto> CreateAsync(CourseRequest request, CancellationToken ct = default)
    {
        var code = request.Code.Trim().ToUpperInvariant();

        if (await _db.Courses.AnyAsync(c => c.Code == code, ct))
            throw new ConflictException($"A course with the code '{code}' already exists.");

        var course = new Course
        {
            Name = request.Name.Trim(),
            Code = code,
            AcademicYear = request.AcademicYear.Trim(),
            IsActive = request.IsActive,
            CreatedAt = _clock.UtcNow
        };

        _db.Courses.Add(course);
        await _db.SaveChangesAsync(ct);

        return new CourseDto(course.Id, course.Name, course.Code, course.AcademicYear, course.IsActive, 0, 0);
    }

    public async Task<CourseDto> UpdateAsync(Guid id, CourseRequest request, CancellationToken ct = default)
    {
        var course = await _db.Courses.FirstOrDefaultAsync(c => c.Id == id, ct)
                     ?? throw NotFoundException.For("Course", id);

        var code = request.Code.Trim().ToUpperInvariant();

        if (await _db.Courses.AnyAsync(c => c.Code == code && c.Id != id, ct))
            throw new ConflictException($"A course with the code '{code}' already exists.");

        course.Name = request.Name.Trim();
        course.Code = code;
        course.AcademicYear = request.AcademicYear.Trim();
        course.IsActive = request.IsActive;

        await _db.SaveChangesAsync(ct);

        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var course = await _db.Courses.FirstOrDefaultAsync(c => c.Id == id, ct)
                     ?? throw NotFoundException.For("Course", id);

        if (await _db.Assignments.AnyAsync(a => a.CourseSubject.CourseId == id, ct))
            throw new ConflictException(
                "The course has assignments and cannot be deleted. Deactivate it instead.");

        _db.Courses.Remove(course);
        await _db.SaveChangesAsync(ct);
    }

    // ------------------------------------------------------- course subjects

    public async Task<IReadOnlyList<CourseSubjectDto>> ListCourseSubjectsAsync(
        Guid courseId, CancellationToken ct = default)
    {
        if (!await _db.Courses.AnyAsync(c => c.Id == courseId, ct))
            throw NotFoundException.For("Course", courseId);

        return await ProjectCourseSubjects(
                _db.CourseSubjects
                    .Where(cs => cs.CourseId == courseId)
                    .OrderBy(cs => cs.Subject.Name))
            .ToListAsync(ct);
    }

    public async Task<CourseSubjectDto> AddSubjectAsync(
        Guid courseId, AddCourseSubjectRequest request, CancellationToken ct = default)
    {
        if (!await _db.Courses.AnyAsync(c => c.Id == courseId, ct))
            throw NotFoundException.For("Course", courseId);

        if (!await _db.Subjects.AnyAsync(s => s.Id == request.SubjectId, ct))
            throw NotFoundException.For("Subject", request.SubjectId);

        if (await _db.CourseSubjects.AnyAsync(cs => cs.CourseId == courseId && cs.SubjectId == request.SubjectId, ct))
            throw new ConflictException("The subject is already part of this course.");

        if (request.TeacherId is { } teacherId)
            await EnsureIsActiveTeacherAsync(teacherId, ct);

        var courseSubject = new CourseSubject
        {
            CourseId = courseId,
            SubjectId = request.SubjectId,
            TeacherId = request.TeacherId,
            CreatedAt = _clock.UtcNow
        };

        _db.CourseSubjects.Add(courseSubject);
        await _db.SaveChangesAsync(ct);

        return await GetCourseSubjectAsync(courseSubject.Id, ct);
    }

    public async Task<CourseSubjectDto> AssignTeacherAsync(
        Guid courseSubjectId, AssignTeacherRequest request, CancellationToken ct = default)
    {
        var courseSubject = await _db.CourseSubjects.FirstOrDefaultAsync(cs => cs.Id == courseSubjectId, ct)
                            ?? throw NotFoundException.For("Course subject", courseSubjectId);

        if (request.TeacherId is { } teacherId)
            await EnsureIsActiveTeacherAsync(teacherId, ct);
        else if (await _db.Assignments.AnyAsync(a => a.CourseSubjectId == courseSubjectId, ct))
            throw new ConflictException(
                "Assignments already exist for this course subject, so it cannot be left without a teacher.");

        courseSubject.TeacherId = request.TeacherId;
        await _db.SaveChangesAsync(ct);

        return await GetCourseSubjectAsync(courseSubjectId, ct);
    }

    public async Task RemoveSubjectAsync(Guid courseSubjectId, CancellationToken ct = default)
    {
        var courseSubject = await _db.CourseSubjects.FirstOrDefaultAsync(cs => cs.Id == courseSubjectId, ct)
                            ?? throw NotFoundException.For("Course subject", courseSubjectId);

        if (await _db.Assignments.AnyAsync(a => a.CourseSubjectId == courseSubjectId, ct))
            throw new ConflictException(
                "The subject has assignments in this course and cannot be removed.");

        _db.CourseSubjects.Remove(courseSubject);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<CourseSubjectDto>> ListTeachableAsync(CancellationToken ct = default)
    {
        var query = _db.CourseSubjects.AsQueryable();

        // Teachers only ever see the pairings an admin handed them; admins see all.
        if (_currentUser.Role == UserRole.Teacher)
        {
            var teacherId = _currentUser.RequireUserId();
            query = query.Where(cs => cs.TeacherId == teacherId);
        }
        else if (_currentUser.Role != UserRole.Admin)
        {
            throw new ForbiddenException("Only teachers and administrators can list teachable course subjects.");
        }

        return await ProjectCourseSubjects(
                query.OrderBy(cs => cs.Course.Name).ThenBy(cs => cs.Subject.Name))
            .ToListAsync(ct);
    }

    // ---------------------------------------------------------- enrollments

    public async Task<IReadOnlyList<EnrollmentDto>> ListEnrollmentsAsync(
        Guid courseId, CancellationToken ct = default)
    {
        if (!await _db.Courses.AnyAsync(c => c.Id == courseId, ct))
            throw NotFoundException.For("Course", courseId);

        return await _db.Enrollments
            .AsNoTracking()
            .Where(e => e.CourseId == courseId)
            .OrderBy(e => e.Student.FullName)
            .Select(e => new EnrollmentDto(
                e.Id, e.CourseId, e.StudentId, e.Student.FullName, e.Student.Email, e.EnrolledAt))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<EnrollmentDto>> EnrollStudentsAsync(
        Guid courseId, EnrollStudentsRequest request, CancellationToken ct = default)
    {
        if (!await _db.Courses.AnyAsync(c => c.Id == courseId, ct))
            throw NotFoundException.For("Course", courseId);

        var requestedIds = request.StudentIds.Distinct().ToList();

        var students = await _db.Users
            .Where(u => requestedIds.Contains(u.Id) && u.Role == UserRole.Student && u.IsActive)
            .Select(u => u.Id)
            .ToListAsync(ct);

        var missing = requestedIds.Except(students).ToList();
        if (missing.Count > 0)
            throw new ValidationException(
                nameof(request.StudentIds),
                $"These ids are not active students: {string.Join(", ", missing)}.");

        var alreadyEnrolled = await _db.Enrollments
            .Where(e => e.CourseId == courseId && requestedIds.Contains(e.StudentId))
            .Select(e => e.StudentId)
            .ToListAsync(ct);

        // Re-enrolling somebody is treated as a no-op rather than an error so the
        // admin UI can send a whole roster without diffing it first.
        var toAdd = students
            .Except(alreadyEnrolled)
            .Select(studentId => new Enrollment
            {
                CourseId = courseId,
                StudentId = studentId,
                EnrolledAt = _clock.UtcNow
            })
            .ToList();

        if (toAdd.Count > 0)
        {
            foreach (var enrollment in toAdd)
                _db.Enrollments.Add(enrollment);

            await _db.SaveChangesAsync(ct);
        }

        return await ListEnrollmentsAsync(courseId, ct);
    }

    public async Task RemoveEnrollmentAsync(Guid courseId, Guid studentId, CancellationToken ct = default)
    {
        var enrollment = await _db.Enrollments
                             .FirstOrDefaultAsync(e => e.CourseId == courseId && e.StudentId == studentId, ct)
                         ?? throw new NotFoundException("The student is not enrolled in this course.");

        // Un-enrolling a student who has already handed work in would orphan their
        // submissions and hide marks they have seen.
        var hasSubmissions = await _db.Submissions
            .AnyAsync(s => s.StudentId == studentId && s.Assignment.CourseSubject.CourseId == courseId, ct);

        if (hasSubmissions)
            throw new ConflictException(
                "The student has submissions in this course and cannot be removed from it.");

        _db.Enrollments.Remove(enrollment);
        await _db.SaveChangesAsync(ct);
    }

    // -------------------------------------------------------------- helpers

    private async Task EnsureIsActiveTeacherAsync(Guid teacherId, CancellationToken ct)
    {
        var isTeacher = await _db.Users
            .AnyAsync(u => u.Id == teacherId && u.Role == UserRole.Teacher && u.IsActive, ct);

        if (!isTeacher)
            throw new ValidationException(
                "TeacherId", "The selected user is not an active teacher.");
    }

    private async Task<CourseSubjectDto> GetCourseSubjectAsync(Guid id, CancellationToken ct) =>
        await ProjectCourseSubjects(_db.CourseSubjects.Where(cs => cs.Id == id)).FirstOrDefaultAsync(ct)
        ?? throw NotFoundException.For("Course subject", id);

    private static IQueryable<CourseSubjectDto> ProjectCourseSubjects(IQueryable<CourseSubject> query) =>
        query.AsNoTracking().Select(cs => new CourseSubjectDto(
            cs.Id,
            cs.CourseId,
            cs.Course.Name,
            cs.Course.Code,
            cs.SubjectId,
            cs.Subject.Name,
            cs.Subject.Code,
            cs.TeacherId,
            cs.Teacher != null ? cs.Teacher.FullName : null));
}
