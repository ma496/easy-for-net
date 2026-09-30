namespace Backend.Features.Notifications.Core.Entities.Configuration;

using Backend.Features.Notifications.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// EF Core entity configuration for <see cref="NotificationVisit"/>. Maps the entity to the
/// "NotificationVisits" table in the "notifications" schema with a single index, the unique
/// (NotificationId, UserId): it serves every per-notification read-state lookup and also backs the
/// cascading foreign key to <see cref="Notification"/>, so no separate foreign-key index is created.
/// </summary>
public class NotificationVisitConfiguration : IEntityTypeConfiguration<NotificationVisit>
{
    public void Configure(EntityTypeBuilder<NotificationVisit> builder)
    {
        builder.ToTable("NotificationVisits", "notifications");

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => new { x.NotificationId, x.UserId }).IsUnique();

        builder.HasOne(x => x.Notification)
               .WithMany(x => x.Visits)
               .HasForeignKey(x => x.NotificationId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
