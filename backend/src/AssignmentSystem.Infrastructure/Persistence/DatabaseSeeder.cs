using AssignmentSystem.Application.Common.Interfaces;
using AssignmentSystem.Domain.Entities;
using AssignmentSystem.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AssignmentSystem.Infrastructure.Persistence;

public interface IDatabaseSeeder
{
    Task SeedAsync(CancellationToken ct = default);
}

/// <summary>
/// Populates the demo dataset described in the README: people, academics and
/// assignments, but no submissions - those are uploaded PDFs, and the seeder has
/// nothing truthful to put in one. Every row is keyed on a fixed id from
/// <see cref="SeedIds"/> and inserted only when missing, so running this on an
/// already-seeded database is a no-op rather than a duplicate-key failure. Deadlines
/// are computed relative to the run time so the demo always has a mix of open,
/// overdue and closed work.
/// </summary>
public class DatabaseSeeder : IDatabaseSeeder
{
    private readonly ApplicationDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<DatabaseSeeder> _logger;
    private readonly SeedOptions _options;

    public DatabaseSeeder(
        ApplicationDbContext db,
        IPasswordHasher passwordHasher,
        IDateTimeProvider clock,
        IOptions<SeedOptions> options,
        ILogger<DatabaseSeeder> logger)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var now = _clock.UtcNow;

        await SeedUsersAsync(now, ct);
        await SeedSubjectsAsync(now, ct);
        await SeedCoursesAsync(now, ct);
        await SeedCourseSubjectsAsync(now, ct);
        await SeedEnrollmentsAsync(now, ct);
        await SeedAssignmentsAsync(now, ct);

        // Submissions are deliberately not seeded. One is a PDF the student uploaded,
        // and inventing a file to stand in for that would put a fabricated document in
        // the repository - so the demo starts with assignments waiting to be answered
        // and the first submission is a real upload.

        _logger.LogInformation("Demo data seeding completed.");
    }

    // ----------------------------------------------------------------- users

    private async Task SeedUsersAsync(DateTime now, CancellationToken ct)
    {
        var users = new[]
        {
            NewUser(SeedIds.Admin, "Ayesha Rahman", "admin@school.edu", UserRole.Admin, _options.AdminPassword, now),
            NewUser(SeedIds.TeacherNazmul, "Nazmul Hasan", "nazmul.hasan@school.edu", UserRole.Teacher, _options.TeacherPassword, now),
            NewUser(SeedIds.TeacherFarhana, "Farhana Akter", "farhana.akter@school.edu", UserRole.Teacher, _options.TeacherPassword, now),
            NewUser(SeedIds.StudentRafi, "Rafi Ahmed", "rafi.ahmed@school.edu", UserRole.Student, _options.StudentPassword, now),
            NewUser(SeedIds.StudentTasnim, "Tasnim Jahan", "tasnim.jahan@school.edu", UserRole.Student, _options.StudentPassword, now),
            NewUser(SeedIds.StudentImran, "Imran Kabir", "imran.kabir@school.edu", UserRole.Student, _options.StudentPassword, now),
            NewUser(SeedIds.StudentNusrat, "Nusrat Sultana", "nusrat.sultana@school.edu", UserRole.Student, _options.StudentPassword, now),
            NewUser(SeedIds.StudentSabbir, "Sabbir Hossain", "sabbir.hossain@school.edu", UserRole.Student, _options.StudentPassword, now)
        };

        await AddMissingAsync(_db.Users, users, u => u.Id, ct);
    }

    private User NewUser(Guid id, string name, string email, UserRole role, string password, DateTime now) =>
        new()
        {
            Id = id,
            FullName = name,
            Email = email,
            Role = role,
            IsActive = true,
            PasswordHash = _passwordHasher.Hash(password),
            CreatedAt = now
        };

    // -------------------------------------------------------------- subjects

    private async Task SeedSubjectsAsync(DateTime now, CancellationToken ct)
    {
        var subjects = new[]
        {
            new Subject { Id = SeedIds.SubjectMath, Name = "Mathematics", Code = "MATH-101", Description = "Algebra, geometry and trigonometry.", CreatedAt = now },
            new Subject { Id = SeedIds.SubjectPhysics, Name = "Physics", Code = "PHY-101", Description = "Mechanics, waves and electricity.", CreatedAt = now },
            new Subject { Id = SeedIds.SubjectComputing, Name = "Computer Science", Code = "CSE-101", Description = "Programming fundamentals and data structures.", CreatedAt = now },
            new Subject { Id = SeedIds.SubjectEnglish, Name = "English", Code = "ENG-101", Description = "Literature and composition.", CreatedAt = now }
        };

        await AddMissingAsync(_db.Subjects, subjects, s => s.Id, ct);
    }

    // --------------------------------------------------------------- courses

    private async Task SeedCoursesAsync(DateTime now, CancellationToken ct)
    {
        var courses = new[]
        {
            new Course { Id = SeedIds.CourseGrade10A, Name = "Grade 10 - Section A", Code = "G10-A", AcademicYear = "2026", IsActive = true, CreatedAt = now },
            new Course { Id = SeedIds.CourseGrade11Science, Name = "Grade 11 - Science", Code = "G11-SCI", AcademicYear = "2026", IsActive = true, CreatedAt = now }
        };

        await AddMissingAsync(_db.Courses, courses, c => c.Id, ct);
    }

    private async Task SeedCourseSubjectsAsync(DateTime now, CancellationToken ct)
    {
        var courseSubjects = new[]
        {
            new CourseSubject { Id = SeedIds.CsGrade10Math, CourseId = SeedIds.CourseGrade10A, SubjectId = SeedIds.SubjectMath, TeacherId = SeedIds.TeacherNazmul, CreatedAt = now },
            new CourseSubject { Id = SeedIds.CsGrade10English, CourseId = SeedIds.CourseGrade10A, SubjectId = SeedIds.SubjectEnglish, TeacherId = SeedIds.TeacherFarhana, CreatedAt = now },
            new CourseSubject { Id = SeedIds.CsGrade11Physics, CourseId = SeedIds.CourseGrade11Science, SubjectId = SeedIds.SubjectPhysics, TeacherId = SeedIds.TeacherFarhana, CreatedAt = now },
            new CourseSubject { Id = SeedIds.CsGrade11Computing, CourseId = SeedIds.CourseGrade11Science, SubjectId = SeedIds.SubjectComputing, TeacherId = SeedIds.TeacherNazmul, CreatedAt = now }
        };

        await AddMissingAsync(_db.CourseSubjects, courseSubjects, cs => cs.Id, ct);
    }

    private async Task SeedEnrollmentsAsync(DateTime now, CancellationToken ct)
    {
        var enrollments = new[]
        {
            new Enrollment { Id = SeedIds.EnrollRafiG10, CourseId = SeedIds.CourseGrade10A, StudentId = SeedIds.StudentRafi, EnrolledAt = now },
            new Enrollment { Id = SeedIds.EnrollTasnimG10, CourseId = SeedIds.CourseGrade10A, StudentId = SeedIds.StudentTasnim, EnrolledAt = now },
            new Enrollment { Id = SeedIds.EnrollImranG10, CourseId = SeedIds.CourseGrade10A, StudentId = SeedIds.StudentImran, EnrolledAt = now },
            // Imran sits in both classes, which makes cross-course filtering visible in the demo.
            new Enrollment { Id = SeedIds.EnrollImranG11, CourseId = SeedIds.CourseGrade11Science, StudentId = SeedIds.StudentImran, EnrolledAt = now },
            new Enrollment { Id = SeedIds.EnrollNusratG11, CourseId = SeedIds.CourseGrade11Science, StudentId = SeedIds.StudentNusrat, EnrolledAt = now },
            new Enrollment { Id = SeedIds.EnrollSabbirG11, CourseId = SeedIds.CourseGrade11Science, StudentId = SeedIds.StudentSabbir, EnrolledAt = now }
        };

        await AddMissingAsync(_db.Enrollments, enrollments, e => e.Id, ct);
    }

    // ----------------------------------------------------------- assignments

    private async Task SeedAssignmentsAsync(DateTime now, CancellationToken ct)
    {
        var assignments = new[]
        {
            // Open, plenty of time left.
            new Assignment
            {
                Id = SeedIds.AssignmentAlgebra,
                CourseSubjectId = SeedIds.CsGrade10Math,
                CreatedByTeacherId = SeedIds.TeacherNazmul,
                Title = "Quadratic Equations Worksheet",
                Description = "Solve the ten quadratic equations in chapter 4 and show each step of your working.",
                MaxMarks = 20m,
                Deadline = now.AddDays(7),
                Status = AssignmentStatus.Published,
                AllowLateSubmission = false,
                AllowResubmission = true,
                CreatedAt = now.AddDays(-3),
                PublishedAt = now.AddDays(-3)
            },
            // Open, due soon.
            new Assignment
            {
                Id = SeedIds.AssignmentEssay,
                CourseSubjectId = SeedIds.CsGrade10English,
                CreatedByTeacherId = SeedIds.TeacherFarhana,
                Title = "Descriptive Essay: A Place I Remember",
                Description = "Write a 500-word descriptive essay about a place that matters to you.",
                MaxMarks = 25m,
                Deadline = now.AddDays(2),
                Status = AssignmentStatus.Published,
                AllowLateSubmission = true,
                AllowResubmission = true,
                CreatedAt = now.AddDays(-5),
                PublishedAt = now.AddDays(-5)
            },
            // Overdue but still accepting work, so late submissions can be demonstrated.
            new Assignment
            {
                Id = SeedIds.AssignmentMotionLab,
                CourseSubjectId = SeedIds.CsGrade11Physics,
                CreatedByTeacherId = SeedIds.TeacherFarhana,
                Title = "Laws of Motion - Lab Report",
                Description = "Submit the lab report for the trolley-and-ramp experiment, including your data table.",
                MaxMarks = 30m,
                Deadline = now.AddDays(-2),
                Status = AssignmentStatus.Published,
                AllowLateSubmission = true,
                AllowResubmission = true,
                CreatedAt = now.AddDays(-12),
                PublishedAt = now.AddDays(-12)
            },
            // Draft: visible to its teacher and admins only.
            new Assignment
            {
                Id = SeedIds.AssignmentSortingDraft,
                CourseSubjectId = SeedIds.CsGrade11Computing,
                CreatedByTeacherId = SeedIds.TeacherNazmul,
                Title = "Sorting Algorithms Comparison",
                Description = "Implement bubble sort and merge sort, then compare their running times on the given inputs.",
                MaxMarks = 40m,
                Deadline = now.AddDays(14),
                Status = AssignmentStatus.Draft,
                AllowLateSubmission = false,
                AllowResubmission = true,
                CreatedAt = now.AddDays(-1)
            },
            // Closed: still readable, no longer accepting work.
            new Assignment
            {
                Id = SeedIds.AssignmentGeometryClosed,
                CourseSubjectId = SeedIds.CsGrade10Math,
                CreatedByTeacherId = SeedIds.TeacherNazmul,
                Title = "Geometry Problem Set",
                Description = "Prove the six geometry statements listed in the handout.",
                MaxMarks = 15m,
                Deadline = now.AddDays(-10),
                Status = AssignmentStatus.Closed,
                AllowLateSubmission = false,
                AllowResubmission = false,
                CreatedAt = now.AddDays(-20),
                PublishedAt = now.AddDays(-20)
            }
        };

        await AddMissingAsync(_db.Assignments, assignments, a => a.Id, ct);
    }

    // -------------------------------------------------------------- helpers

    /// <summary>Inserts only the rows whose ids are not already present.</summary>
    private async Task AddMissingAsync<TEntity>(
        DbSet<TEntity> set,
        IReadOnlyCollection<TEntity> candidates,
        Func<TEntity, Guid> idSelector,
        CancellationToken ct)
        where TEntity : class
    {
        var ids = candidates.Select(idSelector).ToList();

        var existing = await set
            .Where(e => ids.Contains(EF.Property<Guid>(e, "Id")))
            .Select(e => EF.Property<Guid>(e, "Id"))
            .ToListAsync(ct);

        var missing = candidates.Where(c => !existing.Contains(idSelector(c))).ToList();

        if (missing.Count == 0)
            return;

        set.AddRange(missing);
        await _db.SaveChangesAsync(ct);
    }
}

/// <summary>
/// Demo passwords, supplied through configuration so the repository never has to
/// carry a working credential for a real deployment.
/// </summary>
public class SeedOptions
{
    public const string SectionName = "Seed";

    public bool Enabled { get; set; } = true;
    public string AdminPassword { get; set; } = "Admin@123";
    public string TeacherPassword { get; set; } = "Teacher@123";
    public string StudentPassword { get; set; } = "Student@123";
}
