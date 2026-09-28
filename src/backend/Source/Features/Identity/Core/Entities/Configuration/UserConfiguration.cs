namespace Backend.Features.Identity.Core.Entities.Configuration;

using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// EF Core entity configuration for <see cref="User"/>, mapping it to the <c>identity.Users</c> table
/// and defining indexes on the username, email, and name fields (with uniqueness only on the normalized variants).
/// </summary>
public class UserConfiguration : IEntityTypeConfiguration<User>
{
    /// <summary>
    /// Configures the table mapping, schema, and indexes for the user entity's identifying fields.
    /// </summary>
    /// <param name="builder">The builder used to configure the <see cref="User"/> entity type.</param>
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users", "identity");

        builder.HasIndex(u => u.Username)
            .IsUnique(false);
        builder.HasIndex(u => u.UsernameNormalized)
            .IsUnique();
        builder.HasIndex(u => u.Email)
            .IsUnique(false);
        builder.HasIndex(u => u.EmailNormalized)
            .IsUnique();
        builder.HasIndex(u => u.FirstName)
            .IsUnique(false);
        builder.HasIndex(u => u.LastName)
            .IsUnique(false);

        builder.Property(u => u.IsPlatform)
            .HasDefaultValue(false);
    }
}