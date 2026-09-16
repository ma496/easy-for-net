namespace Backend.Features.Identity.Core.Entities.Configuration;

using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// EF Core entity configuration for <see cref="Role"/>, mapping it to the <c>identity.Roles</c> table
/// and enforcing a per-tenant unique index on the normalized role name.
/// </summary>
public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    /// <summary>
    /// Configures the table mapping, schema, and the indexes on the role name and the tenant plus
    /// normalized role name pair.
    /// </summary>
    /// <param name="builder">The builder used to configure the <see cref="Role"/> entity type.</param>
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles", "identity");

        builder.HasIndex(r => r.Name)
            .IsUnique(false);

        // A role name is unique within its tenant, compared on the normalized column so the comparison
        // ignores case and surrounding whitespace. The index is deliberately not filtered on IsDeleted:
        // a name freed only by deleting a role stays reserved within that tenant. Nulls are compared as
        // equal (PostgreSQL 15+ NULLS NOT DISTINCT) so that platform-scoped roles, which carry no tenant,
        // are unique among themselves too instead of escaping the constraint entirely. The database name
        // ends in "Name" because a violation is reported against the last segment, which is the "name"
        // request field. The leading TenantId column also serves every "roles of the active tenant" query.
        builder.HasIndex(r => new { r.TenantId, r.NameNormalized })
            .IsUnique()
            .AreNullsDistinct(false)
            .HasDatabaseName("IX_Roles_TenantId_Name");
    }
}
