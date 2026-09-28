namespace Backend.Features.Notifications.Core.Entities.Configuration;

using Backend.Features.Notifications.Core.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// EF Core entity configuration for <see cref="Notification"/>. Maps the entity to the
/// "Notifications" table in the "notifications" schema, stores <see cref="NotificationType"/>
/// as a string, and defines indexes for lookups by title key, message key, creation date, and by the
/// (tenant, user) pair every notification read is scoped by.
/// </summary>
public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    /// <summary>
    /// Configures the table mapping, schema, the notification type conversion, and the indexes,
    /// including the tenant and user composite every notification read is scoped by.
    /// </summary>
    /// <param name="builder">The builder used to configure the <see cref="Notification"/> entity type.</param>
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications", "notifications");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Type)
               .HasConversion<string>();

        builder.HasIndex(x => x.TitleKey);
        builder.HasIndex(x => x.MessageKey);
        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => new { x.TenantId, x.UserId });
    }
}
