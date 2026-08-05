using AssignmentSystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssignmentSystem.Infrastructure.Persistence.Configurations;

public class AssignmentConfiguration : IEntityTypeConfiguration<Assignment>
{
    public void Configure(EntityTypeBuilder<Assignment> builder)
    {
        builder.ToTable("assignments");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Title).HasMaxLength(200).IsRequired();
        builder.Property(a => a.Description).HasMaxLength(10_000).IsRequired();
        builder.Property(a => a.MaxMarks).HasPrecision(6, 2).IsRequired();
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.HasOne(a => a.CourseSubject)
            .WithMany(cs => cs.Assignments)
            .HasForeignKey(a => a.CourseSubjectId)
            .OnDelete(DeleteBehavior.Cascade);

        // The author is kept for the audit trail even if the pairing is reassigned,
        // so the account cannot be deleted out from under it.
        builder.HasOne(a => a.CreatedByTeacher)
            .WithMany()
            .HasForeignKey(a => a.CreatedByTeacherId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => a.CourseSubjectId);
        builder.HasIndex(a => a.Status);
        builder.HasIndex(a => a.Deadline);
    }
}

public class SubmissionConfiguration : IEntityTypeConfiguration<Submission>
{
    public void Configure(EntityTypeBuilder<Submission> builder)
    {
        builder.ToTable("submissions");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Content).HasMaxLength(20_000).IsRequired();
        builder.Property(s => s.AttachmentUrl).HasMaxLength(2000);
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(s => s.Marks).HasPrecision(6, 2);
        builder.Property(s => s.Feedback).HasMaxLength(5000);
        builder.Property(s => s.AttemptCount).HasDefaultValue(1);

        builder.HasOne(s => s.Assignment)
            .WithMany(a => a.Submissions)
            .HasForeignKey(s => s.AssignmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.Student)
            .WithMany(u => u.Submissions)
            .HasForeignKey(s => s.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.GradedByTeacher)
            .WithMany()
            .HasForeignKey(s => s.GradedByTeacherId)
            .OnDelete(DeleteBehavior.SetNull);

        // Enforces "one submission per student per assignment" in the database as
        // well as in the service, so a double-click cannot create a second row.
        builder.HasIndex(s => new { s.AssignmentId, s.StudentId }).IsUnique();
        builder.HasIndex(s => s.Status);
    }
}
