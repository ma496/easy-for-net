namespace Backend.Features.Identity.Core.Entities.Configuration;

using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// EF Core entity configuration for <see cref="User"/>, mapping it to the <c>identity.Users</c> table
/// and defining indexes on the username, email, and name fields (unique on the username and email, raw and normalized alike).
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

        // Each index is named after its column: ExceptionProcessor reports a unique violation against
        // the last underscore-separated segment with a "Normalized" suffix dropped, so either index of
        // a pair names the same request field.
        builder.HasIndex(u => u.UsernameNormalized)
            .IsUnique()
            .HasDatabaseName("IX_Users_UsernameNormalized");
        builder.HasIndex(u => u.Username)
            .IsUnique()
            .HasDatabaseName("IX_Users_Username");
        builder.HasIndex(u => u.EmailNormalized)
            .IsUnique()
            .HasDatabaseName("IX_Users_EmailNormalized");
        builder.HasIndex(u => u.Email)
            .IsUnique()
            .HasDatabaseName("IX_Users_Email");
        builder.HasIndex(u => u.FirstName)
            .IsUnique(false);
        builder.HasIndex(u => u.LastName)
            .IsUnique(false);

        builder.Property(u => u.IsPlatform)
            .HasDefaultValue(false);
    }
}