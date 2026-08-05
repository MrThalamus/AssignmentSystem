using AssignmentSystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssignmentSystem.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.FullName).HasMaxLength(150).IsRequired();
        builder.Property(u => u.Email).HasMaxLength(256).IsRequired();
        builder.Property(u => u.PasswordHash).HasMaxLength(500).IsRequired();

        // Stored as text: readable in raw SQL and unaffected by re-ordering the enum.
        builder.Property(u => u.Role).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.Property(u => u.IsActive).HasDefaultValue(true);

        // Emails are normalised to lower case before saving, so a plain unique index
        // is enough to make logins unambiguous.
        builder.HasIndex(u => u.Email).IsUnique();
        builder.HasIndex(u => u.Role);
    }
}
