using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Features.Identity.Core.Entities.Configuration;

/// <summary>
/// EF Core entity configuration for the <see cref="UserRole"/> junction, mapping it to <c>identity.UserRoles</c>
/// with a composite key and foreign keys to <see cref="User"/> and <see cref="Role"/>.
/// </summary>
public class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    /// <summary>
    /// Configures the table mapping, composite primary key, and user/role relationships.
    /// </summary>
    /// <param name="builder">The builder used to configure the <see cref="UserRole"/> entity type.</param>
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("UserRoles", "identity");

        builder.HasKey(ur => new { ur.UserId, ur.RoleId });

        builder.HasOne(ur => ur.User)
            .WithMany(u => u.UserRoles)
            .HasForeignKey(ur => ur.UserId);

        builder.HasOne(ur => ur.Role)
            .WithMany(r => r.UserRoles)
            .HasForeignKey(ur => ur.RoleId);
    }
}
