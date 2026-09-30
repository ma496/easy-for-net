namespace Backend.Features.Notifications.Core.Entities.Configuration;

using Backend.Features.Notifications.Core.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// EF Core entity configuration for <see cref="Notification"/>. Maps the entity to the
/// "Notifications" table in the "notifications" schema, stores <see cref="NotificationType"/>
/// as a string, and indexes the (tenant, user) pair every notification read is scoped by, plus two
/// partial indexes over live (not soft-deleted) rows: IX_Notifications_Personal, (TenantId, UserId,
/// IsRead, CreatedAt DESC) over personal notifications, for a user's newest-first list and unread count,
/// and IX_Notifications_Audience, (TenantId, CreatedAt DESC) over audience notifications, for the
/// tenant-wide and platform-wide rows newest first.
/// </summary>
public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    /// <summary>
    /// Configures the table mapping, schema, the notification type conversion, and the indexes.
    /// </summary>
    /// <param name="builder">The builder used to configure the <see cref="Notification"/> entity type.</param>
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications", "notifications");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Type)
               .HasConversion<string>();

        builder.HasIndex(x => new { x.TenantId, x.UserId });

        builder.HasIndex(x => new { x.TenantId, x.UserId, x.IsRead, x.CreatedAt })
               .IsDescending(false, false, false, true)
               .HasFilter("\"UserId\" IS NOT NULL AND NOT \"IsDeleted\"")
               .HasDatabaseName("IX_Notifications_Personal");

        builder.HasIndex(x => new { x.TenantId, x.CreatedAt })
               .IsDescending(false, true)
               .HasFilter("\"UserId\" IS NULL AND NOT \"IsDeleted\"")
               .HasDatabaseName("IX_Notifications_Audience");
    }
}
