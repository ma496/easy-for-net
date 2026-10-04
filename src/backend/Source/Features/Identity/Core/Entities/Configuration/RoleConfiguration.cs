namespace Backend.Features.Identity.Core.Entities.Configuration;

using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// EF Core entity configuration for <see cref="Role"/>, mapping it to the <c>identity.Roles</c> table
/// and enforcing per-tenant unique indexes on the role name and the normalized role name.
/// </summary>
public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    /// <summary>
    /// Configures the table mapping, schema, and the unique indexes on the tenant plus role name and
    /// the tenant plus normalized role name pairs.
    /// </summary>
    /// <param name="builder">The builder used to configure the <see cref="Role"/> entity type.</param>
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles", "identity");

        // The raw name is unique within its tenant on the same terms as the normalized index below.
        builder.HasIndex(r => new { r.TenantId, r.Name })
            .IsUnique()
            .AreNullsDistinct(false)
            .HasDatabaseName("IX_Roles_TenantId_Name");

        // A role name is unique within its tenant, compared on the normalized column so the comparison
        // ignores case and surrounding whitespace. The index is deliberately not filtered on IsDeleted:
        // a name freed only by deleting a role stays reserved within that tenant. Nulls are compared as
        // equal (PostgreSQL 15+ NULLS NOT DISTINCT) so that platform-scoped roles, which carry no tenant,
        // are unique among themselves too instead of escaping the constraint entirely. A violation is
        // reported against the last segment with "Normalized" dropped, which is the "name" request
        // field. The leading TenantId column also serves every "roles of the active tenant" query.
        builder.HasIndex(r => new { r.TenantId, r.NameNormalized })
            .IsUnique()
            .AreNullsDistinct(false)
            .HasDatabaseName("IX_Roles_TenantId_NameNormalized");
    }
}
