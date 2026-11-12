using Backend.Features.Notifications.Core.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Features.Notifications.Core.Entities.Configuration;

/// <summary>
/// EF Core entity configuration for <see cref="NotificationReadCursor"/>. Maps the entity to the
/// "NotificationReadCursors" table in the "notifications" schema and makes UserId unique, so a user has
/// exactly one cursor.
/// </summary>
public class NotificationReadCursorConfiguration : IEntityTypeConfiguration<NotificationReadCursor>
{
    public void Configure(EntityTypeBuilder<NotificationReadCursor> builder)
    {
        builder.ToTable("NotificationReadCursors", "notifications");

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.UserId)
               .IsUnique();
    }
}
