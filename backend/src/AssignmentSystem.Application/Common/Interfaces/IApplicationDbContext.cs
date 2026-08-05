using AssignmentSystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AssignmentSystem.Application.Common.Interfaces;

/// <summary>
/// The persistence surface the application layer is allowed to touch. Exposing the
/// DbSets (rather than a repository per entity) keeps LINQ composition and eager
/// loading available while still letting tests swap in an in-memory context.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<Subject> Subjects { get; }
    DbSet<Course> Courses { get; }
    DbSet<CourseSubject> CourseSubjects { get; }
    DbSet<Enrollment> Enrollments { get; }
    DbSet<Assignment> Assignments { get; }
    DbSet<Submission> Submissions { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
