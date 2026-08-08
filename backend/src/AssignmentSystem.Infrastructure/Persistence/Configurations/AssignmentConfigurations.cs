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

        builder.Property(s => s.FileName).HasMaxLength(255).IsRequired();
        builder.Property(s => s.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(s => s.FileSizeBytes).IsRequired();
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

        // The PDF hangs off the submission one-to-one and dies with it, so a deleted
        // assignment cannot leave orphaned blobs behind.
        builder.HasOne(s => s.File)
            .WithOne(f => f.Submission)
            .HasForeignKey<SubmissionFile>(f => f.SubmissionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Enforces "one submission per student per assignment" in the database as
        // well as in the service, so a double-click cannot create a second row.
        builder.HasIndex(s => new { s.AssignmentId, s.StudentId }).IsUnique();
        builder.HasIndex(s => s.Status);
    }
}

public class SubmissionFileConfiguration : IEntityTypeConfiguration<SubmissionFile>
{
    public void Configure(EntityTypeBuilder<SubmissionFile> builder)
    {
        builder.ToTable("submission_files");

        // The submission's own id doubles as the key: there is never more than one
        // file per submission, so a surrogate would only add a second thing to join on.
        builder.HasKey(f => f.SubmissionId);

        builder.Property(f => f.Content).IsRequired();
    }
}
