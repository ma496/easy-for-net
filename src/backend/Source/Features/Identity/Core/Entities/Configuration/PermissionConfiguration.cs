namespace Backend.Features.Identity.Core.Entities.Configuration;

using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// EF Core entity configuration for <see cref="Permission"/>, mapping it to the <c>identity.Permissions</c> table
/// and enforcing a unique name index.
/// </summary>
public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    /// <summary>
    /// Configures the table mapping, schema, and unique index on the permission name.
    /// </summary>
    /// <param name="builder">The builder used to configure the <see cref="Permission"/> entity type.</param>
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("Permissions", "identity");

        builder.HasIndex(p => p.Name)
            .IsUnique();

        // Stored as the underlying integer so the per-request scope narrowing is a plain comparison
        // the provider can translate.
        builder.Property(p => p.Scope)
            .HasConversion<int>()
            .HasDefaultValue(PermissionScope.Tenant);
    }
}
