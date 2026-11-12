using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Features.Identity.Core.Entities.Configuration;

/// <summary>
/// EF Core entity configuration for the <see cref="RolePermission"/> junction, mapping it to <c>identity.RolePermissions</c>
/// with a composite key and foreign keys to <see cref="Role"/> and <see cref="Permission"/>.
/// </summary>
public class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    /// <summary>
    /// Configures the table mapping, composite primary key, and role/permission relationships.
    /// </summary>
    /// <param name="builder">The builder used to configure the <see cref="RolePermission"/> entity type.</param>
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("RolePermissions", "identity");

        builder.HasKey(rp => new { rp.RoleId, rp.PermissionId });

        builder.HasOne(rp => rp.Role)
            .WithMany(r => r.RolePermissions)
            .HasForeignKey(rp => rp.RoleId);

        builder.HasOne(rp => rp.Permission)
            .WithMany(p => p.RolePermissions)
            .HasForeignKey(rp => rp.PermissionId);
    }
}
