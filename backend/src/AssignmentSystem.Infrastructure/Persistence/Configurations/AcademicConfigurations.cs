using AssignmentSystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssignmentSystem.Infrastructure.Persistence.Configurations;

public class SubjectConfiguration : IEntityTypeConfiguration<Subject>
{
    public void Configure(EntityTypeBuilder<Subject> builder)
    {
        builder.ToTable("subjects");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Name).HasMaxLength(150).IsRequired();
        builder.Property(s => s.Code).HasMaxLength(30).IsRequired();
        builder.Property(s => s.Description).HasMaxLength(1000);

        builder.HasIndex(s => s.Code).IsUnique();
    }
}

public class CourseConfiguration : IEntityTypeConfiguration<Course>
{
    public void Configure(EntityTypeBuilder<Course> builder)
    {
        builder.ToTable("courses");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Name).HasMaxLength(150).IsRequired();
        builder.Property(c => c.Code).HasMaxLength(30).IsRequired();
        builder.Property(c => c.AcademicYear).HasMaxLength(20).IsRequired();
        builder.Property(c => c.IsActive).HasDefaultValue(true);

        builder.HasIndex(c => c.Code).IsUnique();
    }
}

public class CourseSubjectConfiguration : IEntityTypeConfiguration<CourseSubject>
{
    public void Configure(EntityTypeBuilder<CourseSubject> builder)
    {
        builder.ToTable("course_subjects");

        builder.HasKey(cs => cs.Id);

        builder.HasOne(cs => cs.Course)
            .WithMany(c => c.CourseSubjects)
            .HasForeignKey(cs => cs.CourseId)
            .OnDelete(DeleteBehavior.Cascade);

        // Blocking the delete keeps a subject from silently taking assignments with
        // it; the service reports the conflict instead.
        builder.HasOne(cs => cs.Subject)
            .WithMany(s => s.CourseSubjects)
            .HasForeignKey(cs => cs.SubjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(cs => cs.Teacher)
            .WithMany(u => u.TeachingAssignments)
            .HasForeignKey(cs => cs.TeacherId)
            .OnDelete(DeleteBehavior.Restrict);

        // A subject is taught once per course, by one teacher.
        builder.HasIndex(cs => new { cs.CourseId, cs.SubjectId }).IsUnique();
        builder.HasIndex(cs => cs.TeacherId);
    }
}

public class EnrollmentConfiguration : IEntityTypeConfiguration<Enrollment>
{
    public void Configure(EntityTypeBuilder<Enrollment> builder)
    {
        builder.ToTable("enrollments");

        builder.HasKey(e => e.Id);

        builder.HasOne(e => e.Course)
            .WithMany(c => c.Enrollments)
            .HasForeignKey(e => e.CourseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Student)
            .WithMany(u => u.Enrollments)
            .HasForeignKey(e => e.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.CourseId, e.StudentId }).IsUnique();
    }
}
