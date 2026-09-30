namespace Backend.Features.Notifications.Core.Entities.Configuration;

using Backend.Features.Notifications.Core.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// EF Core entity configuration for <see cref="NotificationReadCursor"/>. Maps the entity to the
/// "NotificationReadCursors" table in the "notifications" schema and makes (UserId, TenantId) unique
/// with nulls not distinct, so a user has one cursor per tenant and exactly one platform-wide cursor.
/// </summary>
public class NotificationReadCursorConfiguration : IEntityTypeConfiguration<NotificationReadCursor>
{
    public void Configure(EntityTypeBuilder<NotificationReadCursor> builder)
    {
        builder.ToTable("NotificationReadCursors", "notifications");

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => new { x.UserId, x.TenantId })
               .IsUnique()
               .AreNullsDistinct(false);
    }
}
