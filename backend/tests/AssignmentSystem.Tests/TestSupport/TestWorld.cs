using AssignmentSystem.Application.Features.Academics;
using AssignmentSystem.Application.Features.Assignments;
using AssignmentSystem.Application.Features.Submissions;
using AssignmentSystem.Application.Features.Users;
using AssignmentSystem.Domain.Entities;
using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Infrastructure.Persistence;
using AssignmentSystem.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace AssignmentSystem.Tests.TestSupport;

/// <summary>
/// A small, fully-wired school used by the service tests: two teachers with separate
/// course subjects, three students with different enrollments, and assignments in
/// every interesting state. Each test gets its own in-memory database, so tests stay
/// independent and can be run in any order.
/// </summary>
public sealed class TestWorld : IDisposable
{
    // Deliberately a fixed instant: every deadline below is expressed relative to it.
    public static readonly DateTime Start = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);

    public ApplicationDbContext Db { get; }
    public FakeClock Clock { get; } = new(Start);
    public StubCurrentUser CurrentUser { get; } = new();

    // people
    public Guid AdminId { get; } = Guid.NewGuid();
    public Guid TeacherAId { get; } = Guid.NewGuid();
    public Guid TeacherBId { get; } = Guid.NewGuid();
    public Guid StudentAId { get; } = Guid.NewGuid();
    public Guid StudentBId { get; } = Guid.NewGuid();

    /// <summary>Enrolled in the physics course only - used to prove course scoping.</summary>
    public Guid StudentCId { get; } = Guid.NewGuid();

    // academics
    public Guid MathsCourseId { get; } = Guid.NewGuid();
    public Guid PhysicsCourseId { get; } = Guid.NewGuid();
    public Guid MathsSubjectId { get; } = Guid.NewGuid();
    public Guid PhysicsSubjectId { get; } = Guid.NewGuid();

    /// <summary>Maths for the maths course, taught by teacher A.</summary>
    public Guid MathsCourseSubjectId { get; } = Guid.NewGuid();

    /// <summary>Physics for the physics course, taught by teacher B.</summary>
    public Guid PhysicsCourseSubjectId { get; } = Guid.NewGuid();

    /// <summary>Has no teacher assigned, so no assignment may be created for it.</summary>
    public Guid UnassignedCourseSubjectId { get; } = Guid.NewGuid();

    // assignments (all teacher A / maths unless noted)
    public Guid OpenAssignmentId { get; } = Guid.NewGuid();
    public Guid DraftAssignmentId { get; } = Guid.NewGuid();
    public Guid LateAllowedAssignmentId { get; } = Guid.NewGuid();
    public Guid NoResubmitAssignmentId { get; } = Guid.NewGuid();
    public Guid ClosedAssignmentId { get; } = Guid.NewGuid();

    /// <summary>Belongs to teacher B, in the physics course.</summary>
    public Guid OtherTeacherAssignmentId { get; } = Guid.NewGuid();

    public TestWorld()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"assignment-tests-{Guid.NewGuid()}")
            // The in-memory provider cannot honour the model's relational bits; the
            // warning is expected and not something a test should fail on.
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics
                .InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        Db = new ApplicationDbContext(options);
        Populate();
    }

    // ------------------------------------------------- services under test

    public AssignmentService Assignments() => new(Db, CurrentUser, Clock);

    public SubmissionService Submissions() => new(Db, CurrentUser, Clock);

    public UserService Users() => new(Db, new Pbkdf2PasswordHasher(), CurrentUser, Clock);

    public CourseService Courses() => new(Db, CurrentUser, Clock);

    public SubjectService Subjects() => new(Db, Clock);

    // ------------------------------------------------------ identity helpers

    public TestWorld AsAdmin() { CurrentUser.SignInAs(AdminId, UserRole.Admin); return this; }
    public TestWorld AsTeacherA() { CurrentUser.SignInAs(TeacherAId, UserRole.Teacher); return this; }
    public TestWorld AsTeacherB() { CurrentUser.SignInAs(TeacherBId, UserRole.Teacher); return this; }
    public TestWorld AsStudentA() { CurrentUser.SignInAs(StudentAId, UserRole.Student); return this; }
    public TestWorld AsStudentB() { CurrentUser.SignInAs(StudentBId, UserRole.Student); return this; }
    public TestWorld AsStudentC() { CurrentUser.SignInAs(StudentCId, UserRole.Student); return this; }

    // ------------------------------------------------------------- fixtures

    public Assignment Assignment(Guid id) => Db.Assignments.Single(a => a.Id == id);

    /// <summary>Adds a submission directly, bypassing the service, to set up a scenario.</summary>
    public Submission GiveSubmission(
        Guid assignmentId,
        Guid studentId,
        SubmissionStatus status = SubmissionStatus.Submitted,
        decimal? marks = null,
        bool isLate = false)
    {
        var submission = new Submission
        {
            AssignmentId = assignmentId,
            StudentId = studentId,
            Content = "Existing answer.",
            Status = status,
            IsLate = isLate,
            AttemptCount = 1,
            SubmittedAt = Clock.UtcNow,
            Marks = marks,
            GradedAt = marks is null ? null : Clock.UtcNow,
            GradedByTeacherId = marks is null ? null : TeacherAId
        };

        Db.Submissions.Add(submission);
        Db.SaveChanges();

        return submission;
    }

    private void Populate()
    {
        Db.Users.AddRange(
            User(AdminId, "Admin User", "admin@test.edu", UserRole.Admin),
            User(TeacherAId, "Teacher A", "teacher.a@test.edu", UserRole.Teacher),
            User(TeacherBId, "Teacher B", "teacher.b@test.edu", UserRole.Teacher),
            User(StudentAId, "Student A", "student.a@test.edu", UserRole.Student),
            User(StudentBId, "Student B", "student.b@test.edu", UserRole.Student),
            User(StudentCId, "Student C", "student.c@test.edu", UserRole.Student));

        Db.Subjects.AddRange(
            new Subject { Id = MathsSubjectId, Name = "Mathematics", Code = "MATH-101", CreatedAt = Start },
            new Subject { Id = PhysicsSubjectId, Name = "Physics", Code = "PHY-101", CreatedAt = Start });

        Db.Courses.AddRange(
            new Course { Id = MathsCourseId, Name = "Grade 10 - A", Code = "G10-A", AcademicYear = "2026", IsActive = true, CreatedAt = Start },
            new Course { Id = PhysicsCourseId, Name = "Grade 11 - Science", Code = "G11-SCI", AcademicYear = "2026", IsActive = true, CreatedAt = Start });

        Db.CourseSubjects.AddRange(
            new CourseSubject { Id = MathsCourseSubjectId, CourseId = MathsCourseId, SubjectId = MathsSubjectId, TeacherId = TeacherAId, CreatedAt = Start },
            new CourseSubject { Id = PhysicsCourseSubjectId, CourseId = PhysicsCourseId, SubjectId = PhysicsSubjectId, TeacherId = TeacherBId, CreatedAt = Start },
            new CourseSubject { Id = UnassignedCourseSubjectId, CourseId = MathsCourseId, SubjectId = PhysicsSubjectId, TeacherId = null, CreatedAt = Start });

        Db.Enrollments.AddRange(
            new Enrollment { CourseId = MathsCourseId, StudentId = StudentAId, EnrolledAt = Start },
            new Enrollment { CourseId = MathsCourseId, StudentId = StudentBId, EnrolledAt = Start },
            new Enrollment { CourseId = PhysicsCourseId, StudentId = StudentCId, EnrolledAt = Start });

        Db.Assignments.AddRange(
            NewAssignment(OpenAssignmentId, MathsCourseSubjectId, TeacherAId, "Open worksheet",
                AssignmentStatus.Published, Start.AddDays(7), allowLate: false, allowResubmit: true),

            NewAssignment(DraftAssignmentId, MathsCourseSubjectId, TeacherAId, "Unpublished draft",
                AssignmentStatus.Draft, Start.AddDays(7), allowLate: false, allowResubmit: true),

            NewAssignment(LateAllowedAssignmentId, MathsCourseSubjectId, TeacherAId, "Late submissions allowed",
                AssignmentStatus.Published, Start.AddDays(1), allowLate: true, allowResubmit: true),

            NewAssignment(NoResubmitAssignmentId, MathsCourseSubjectId, TeacherAId, "One attempt only",
                AssignmentStatus.Published, Start.AddDays(7), allowLate: false, allowResubmit: false),

            NewAssignment(ClosedAssignmentId, MathsCourseSubjectId, TeacherAId, "Closed set",
                AssignmentStatus.Closed, Start.AddDays(-5), allowLate: true, allowResubmit: true),

            NewAssignment(OtherTeacherAssignmentId, PhysicsCourseSubjectId, TeacherBId, "Physics lab report",
                AssignmentStatus.Published, Start.AddDays(3), allowLate: false, allowResubmit: true));

        Db.SaveChanges();
    }

    private static User User(Guid id, string name, string email, UserRole role) => new()
    {
        Id = id,
        FullName = name,
        Email = email,
        // Tests that need a real hash create it through the hasher; nothing here logs in.
        PasswordHash = "not-used-in-tests",
        Role = role,
        IsActive = true,
        CreatedAt = Start
    };

    private static Assignment NewAssignment(
        Guid id, Guid courseSubjectId, Guid teacherId, string title,
        AssignmentStatus status, DateTime deadline, bool allowLate, bool allowResubmit) => new()
    {
        Id = id,
        CourseSubjectId = courseSubjectId,
        CreatedByTeacherId = teacherId,
        Title = title,
        Description = $"{title} description.",
        MaxMarks = 20m,
        Deadline = deadline,
        Status = status,
        AllowLateSubmission = allowLate,
        AllowResubmission = allowResubmit,
        CreatedAt = Start,
        PublishedAt = status == AssignmentStatus.Draft ? null : Start
    };

    public void Dispose() => Db.Dispose();
}
