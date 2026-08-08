using AssignmentSystem.Domain.Entities;
using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Infrastructure.Persistence;
using AssignmentSystem.Infrastructure.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AssignmentSystem.Tests.TestSupport;

/// <summary>
/// Boots the real API in memory, backed by SQLite instead of PostgreSQL.
///
/// The service tests call the services directly, which leaves the layer between HTTP
/// and the service untested - model binding in particular. That matters most for the
/// submission endpoints, where the payload is multipart form data rather than JSON, so
/// this exists to exercise a genuine request over the wire.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public const string StudentEmail = "student@test.edu";
    public const string TeacherEmail = "teacher@test.edu";
    public const string Password = "Sup3rSecret!";

    public Guid AssignmentId { get; } = Guid.NewGuid();
    public Guid ClosedAssignmentId { get; } = Guid.NewGuid();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:DefaultConnection", "Host=unused;Database=unused");

        // The host would otherwise try to migrate and seed PostgreSQL on startup.
        builder.UseSetting("Database:AutoMigrate", "false");
        builder.UseSetting("Seed:Enabled", "false");

        // Long enough to clear the minimum the API refuses to start without.
        builder.UseSetting("Jwt:Key", new string('k', 64));
        builder.UseSetting("Jwt:Issuer", "AssignmentSystem");
        builder.UseSetting("Jwt:Audience", "AssignmentSystemClient");

        builder.ConfigureServices(services =>
        {
            // Swap Npgsql for a SQLite database that lives as long as the connection.
            //
            // Dropping the options object is not enough. AddDbContext registers an
            // options *configuration* that is re-applied every time the options are
            // built, so calling AddDbContext again would layer SQLite on top of Npgsql
            // and leave the context holding two providers, which EF refuses to start
            // with. The first registration has to be withdrawn, along with the provider
            // services it brought with it.
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<DbContextOptions>();

            foreach (var descriptor in services
                         .Where(d => IsOptionsConfiguration(d) || IsNpgsqlService(d))
                         .ToList())
            {
                services.Remove(descriptor);
            }

            _connection.Open();
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(_connection));
        });
    }

    /// <summary>
    /// The per-context options configuration registered by <c>AddDbContext</c>. Matched
    /// by name because the interface is not part of the public surface in every version.
    /// </summary>
    private static bool IsOptionsConfiguration(ServiceDescriptor descriptor) =>
        descriptor.ServiceType.FullName?.Contains("IDbContextOptionsConfiguration", StringComparison.Ordinal)
        == true;

    /// <summary>
    /// Matches on type names rather than assemblies. The registration that actually
    /// makes EF see a second provider is a relational type closed over an Npgsql one -
    /// <c>DatabaseProvider&lt;NpgsqlOptionsExtension&gt;</c> - which lives in EF's own
    /// assembly and so survives any assembly-level filter.
    /// </summary>
    private static bool IsNpgsqlService(ServiceDescriptor descriptor) =>
        Names(descriptor).Any(name => name?.Contains("Npgsql", StringComparison.Ordinal) == true);

    private static IEnumerable<string?> Names(ServiceDescriptor descriptor)
    {
        yield return descriptor.ServiceType.FullName;
        yield return descriptor.ImplementationType?.FullName;
        yield return descriptor.ImplementationInstance?.GetType().FullName;
    }

    private readonly Lock _gate = new();
    private bool _prepared;

    /// <summary>
    /// A client against a database that holds the fixture the submission endpoints need.
    /// The database is shared by every test in the class - it is created and populated
    /// once, because seeding it again would collide with its own unique constraints.
    /// </summary>
    public HttpClient CreateConfiguredClient()
    {
        var client = CreateClient();

        lock (_gate)
        {
            if (_prepared) return client;

            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            db.Database.EnsureCreated();
            Populate(db);

            _prepared = true;
        }

        return client;
    }

    private void Populate(ApplicationDbContext db)
    {
        var hasher = new Pbkdf2PasswordHasher();
        var now = new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);

        var teacher = new User
        {
            FullName = "Teacher One",
            Email = TeacherEmail,
            PasswordHash = hasher.Hash(Password),
            Role = UserRole.Teacher,
            IsActive = true,
            CreatedAt = now
        };

        var student = new User
        {
            FullName = "Student One",
            Email = StudentEmail,
            PasswordHash = hasher.Hash(Password),
            Role = UserRole.Student,
            IsActive = true,
            CreatedAt = now
        };

        var subject = new Subject { Name = "Mathematics", Code = "MATH-101", CreatedAt = now };
        var course = new Course
        {
            Name = "Grade 10 - A",
            Code = "G10-A",
            AcademicYear = "2026",
            IsActive = true,
            CreatedAt = now
        };

        db.Users.AddRange(teacher, student);
        db.Subjects.Add(subject);
        db.Courses.Add(course);
        db.SaveChanges();

        var courseSubject = new CourseSubject
        {
            CourseId = course.Id,
            SubjectId = subject.Id,
            TeacherId = teacher.Id,
            CreatedAt = now
        };

        db.CourseSubjects.Add(courseSubject);
        db.Enrollments.Add(new Enrollment
        {
            CourseId = course.Id,
            StudentId = student.Id,
            EnrolledAt = now
        });
        db.SaveChanges();

        db.Assignments.AddRange(
            new Assignment
            {
                Id = AssignmentId,
                CourseSubjectId = courseSubject.Id,
                CreatedByTeacherId = teacher.Id,
                Title = "Open worksheet",
                Description = "Hand in the worksheet as a PDF.",
                MaxMarks = 20m,
                // Far enough ahead that the wall clock cannot overtake it.
                Deadline = DateTime.UtcNow.AddYears(5),
                Status = AssignmentStatus.Published,
                AllowLateSubmission = false,
                AllowResubmission = true,
                CreatedAt = now,
                PublishedAt = now
            },
            new Assignment
            {
                Id = ClosedAssignmentId,
                CourseSubjectId = courseSubject.Id,
                CreatedByTeacherId = teacher.Id,
                Title = "Closed worksheet",
                Description = "No longer accepting work.",
                MaxMarks = 20m,
                Deadline = DateTime.UtcNow.AddYears(-1),
                Status = AssignmentStatus.Closed,
                AllowLateSubmission = true,
                AllowResubmission = true,
                CreatedAt = now,
                PublishedAt = now
            });

        db.SaveChanges();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing) _connection.Dispose();
    }
}
