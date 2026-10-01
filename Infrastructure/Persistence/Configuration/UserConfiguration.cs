using Domain.Users;
using Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configuration;

/// <summary>
/// Mapping for the user row. Kept out of <c>Program.cs</c> and out of the domain on purpose:
/// this is the file that knows the table has a <c>Status</c> column, and the domain must not.
/// </summary>
public class UserConfiguration : IEntityTypeConfiguration<UserEntity>
{
    public void Configure(EntityTypeBuilder<UserEntity> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id)
            .ValueGeneratedNever();

        // Normalized in the domain's Email.Create, so a case-variant address can never
        // occupy a second row. The unique index is what actually enforces account
        // uniqueness; the lower-casing is only what makes the lookup deterministic.
        builder.Property(u => u.Email)
            .HasMaxLength(Email.MaxLength)
            .IsRequired();

        builder.HasIndex(u => u.Email)
            .IsUnique()
            .HasDatabaseName("IX_Users_Email");

        builder.Property(u => u.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(u => u.PasswordHash)
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(u => u.RolesJson)
            .IsRequired();

        builder.Property(u => u.RegisteredAt)
            .IsRequired();

        builder.Property(u => u.VerificationTokenHash)
            .HasMaxLength(256);

        builder.Property(u => u.DeactivationReason)
            .HasMaxLength(512);

        // SignedInAttempts as a column named after the domain meaning, not the storage detail.
        builder.Property(u => u.FailedSignInAttempts)
            .HasDefaultValue(0);
    }
}
