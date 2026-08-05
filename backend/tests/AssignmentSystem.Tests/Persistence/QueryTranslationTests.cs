using AssignmentSystem.Application.Features.Academics;
using AssignmentSystem.Application.Features.Assignments;
using AssignmentSystem.Application.Features.Submissions;
using AssignmentSystem.Application.Features.Users;
using AssignmentSystem.Domain.Entities;
using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Infrastructure.Persistence;
using AssignmentSystem.Infrastructure.Security;
using AssignmentSystem.Tests.TestSupport;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AssignmentSystem.Tests.Persistence;

/// <summary>
/// Runs every list query against a real relational provider.
///
/// The rest of the suite uses the in-memory provider, which evaluates LINQ as plain
/// objects and therefore accepts expressions no database can translate - an ordering
/// applied after projecting to a DTO, for example, passes in memory and then throws
/// at runtime against PostgreSQL. These tests exist to close that gap: SQLite has to
/// compile each query to SQL, so an untranslatable one fails here instead of in
/// production.
///
/// SQLite is not PostgreSQL, so this proves the queries translate, not that they
/// behave identically. Correctness of the results is covered by the service tests.
/// </summary>
public sealed class QueryTranslationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ApplicationDbContext _db;
    private readonly StubCurrentUser _currentUser = new();
    private readonly FakeClock _clock = new();

    public QueryTranslationTests()
    {
        // A shared in-memory SQLite database lives as long as the connection does.
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new ApplicationDbContext(options);
        _db.Database.EnsureCreated();
    }

    private AssignmentService Assignments() => new(_db, _currentUser, _clock);
    private SubmissionService Submissions() => new(_db, _currentUser, _clock);
    private CourseService Courses() => new(_db, _currentUser, _clock);
    private SubjectService Subjects() => new(_db, _clock);
    private UserService Users() => new(_db, new Pbkdf2PasswordHasher(), _currentUser, _clock);

    private void SignInAs(UserRole role) => _currentUser.SignInAs(Guid.NewGuid(), role);

    // ----------------------------------------------------------- assignments

    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.Teacher)]
    [InlineData(UserRole.Student)]
    public async Task Listing_assignments_translates_for_every_role(UserRole role)
    {
        SignInAs(role);

        await Assignments().ListAsync(new AssignmentListQuery());
    }

    [Fact]
    public async Task Listing_assignments_translates_with_every_filter_applied()
    {
        SignInAs(UserRole.Student);

        await Assignments().ListAsync(new AssignmentListQuery
        {
            CourseId = Guid.NewGuid(),
            SubjectId = Guid.NewGuid(),
            Status = AssignmentStatus.Published,
            Search = "worksheet",
            OnlyPending = true,
            DueAfter = _clock.UtcNow,
            DueBefore = _clock.UtcNow.AddDays(30),
            Page = 2,
            PageSize = 5
        });
    }

    // ----------------------------------------------------------- submissions

    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.Teacher)]
    [InlineData(UserRole.Student)]
    public async Task Listing_submissions_translates_for_every_role(UserRole role)
    {
        SignInAs(role);

        await Submissions().ListAsync(new SubmissionListQuery());
    }

    [Fact]
    public async Task Listing_submissions_translates_with_every_filter_applied()
    {
        SignInAs(UserRole.Admin);

        await Submissions().ListAsync(new SubmissionListQuery
        {
            AssignmentId = Guid.NewGuid(),
            CourseId = Guid.NewGuid(),
            StudentId = Guid.NewGuid(),
            Status = SubmissionStatus.Graded,
            PageSize = 50
        });
    }

    // -------------------------------------------------------------- academics

    [Fact]
    public async Task Listing_users_translates_with_every_filter_applied()
    {
        SignInAs(UserRole.Admin);

        await Users().ListAsync(new UserListQuery
        {
            Role = UserRole.Teacher,
            IsActive = true,
            Search = "hasan"
        });
    }

    [Fact]
    public async Task Listing_courses_translates_with_every_filter_applied()
    {
        SignInAs(UserRole.Admin);

        await Courses().ListAsync(new CourseListQuery { IsActive = true, Search = "grade" });
    }

    [Fact]
    public async Task Listing_subjects_translates()
    {
        SignInAs(UserRole.Admin);

        await Subjects().ListAsync();
    }

    /// <summary>
    /// This query is the reason the class exists: it projects to a DTO and used to
    /// order by one of the DTO's properties, which no relational provider can turn
    /// into SQL.
    /// </summary>
    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.Teacher)]
    public async Task Listing_teachable_course_subjects_translates(UserRole role)
    {
        SignInAs(role);

        await Courses().ListTeachableAsync();
    }

    [Fact]
    public async Task Listing_the_subjects_of_a_course_translates()
    {
        SignInAs(UserRole.Admin);

        var course = new Course
        {
            Name = "Grade 10 - A",
            Code = "G10-A",
            AcademicYear = "2026",
            CreatedAt = _clock.UtcNow
        };

        _db.Courses.Add(course);
        await _db.SaveChangesAsync();

        await Courses().ListCourseSubjectsAsync(course.Id);
        await Courses().ListEnrollmentsAsync(course.Id);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
