using Backend.Features.Notifications.Core.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Features.Notifications.Core.Entities.Configuration;

/// <summary>
/// EF Core entity configuration for <see cref="Notification"/>. Maps the entity to the
/// "Notifications" table in the "notifications" schema, stores <see cref="NotificationType"/>
/// as a string, and adds two partial indexes over live (not soft-deleted) rows:
/// IX_Notifications_Personal, (UserId, IsRead, CreatedAt DESC) over personal notifications, for a
/// user's newest-first list and unread count, and IX_Notifications_Broadcast, (CreatedAt DESC) over the
/// all-users broadcasts, newest first.
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

        builder.HasIndex(x => new { x.UserId, x.IsRead, x.CreatedAt })
               .IsDescending(false, false, true)
               .HasFilter("\"UserId\" IS NOT NULL AND NOT \"IsDeleted\"")
               .HasDatabaseName("IX_Notifications_Personal");

        builder.HasIndex(x => x.CreatedAt)
               .IsDescending(true)
               .HasFilter("\"UserId\" IS NULL AND NOT \"IsDeleted\"")
               .HasDatabaseName("IX_Notifications_Broadcast");
    }
}
