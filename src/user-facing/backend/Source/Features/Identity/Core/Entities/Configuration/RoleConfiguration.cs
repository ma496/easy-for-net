using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Features.Identity.Core.Entities.Configuration;

/// <summary>
/// EF Core entity configuration for <see cref="Role"/>, mapping it to the <c>identity.Roles</c> table
/// and enforcing unique indexes on the role name and the normalized role name.
/// </summary>
public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    /// <summary>
    /// Configures the table mapping, schema, and the unique indexes on the role name and the
    /// normalized role name.
    /// </summary>
    /// <param name="builder">The builder used to configure the <see cref="Role"/> entity type.</param>
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles", "identity");

        builder.HasIndex(r => r.Name)
            .IsUnique()
            .HasDatabaseName("IX_Roles_Name");

        // A role name is unique, compared on the normalized column so the comparison ignores case and
        // surrounding whitespace. The index is deliberately not filtered on IsDeleted: a name freed only
        // by deleting a role stays reserved. A violation is reported against the last segment with
        // "Normalized" dropped, which is the "name" request field.
        builder.HasIndex(r => r.NameNormalized)
            .IsUnique()
            .HasDatabaseName("IX_Roles_NameNormalized");
    }
}
