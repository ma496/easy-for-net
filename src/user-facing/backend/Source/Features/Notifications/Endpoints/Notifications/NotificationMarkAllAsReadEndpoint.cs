using Backend.Features.Identity.Core;
using Backend.Features.Notifications.Core;
using Backend.Features.Notifications.Core.Push;

namespace Backend.Features.Notifications.Endpoints.Notifications;

/// <summary>
/// POST endpoint that marks every notification the current user can see as read: their own unread
/// notifications and the all-users broadcasts.
/// </summary>
/// <remarks>
/// The work is three bulk statements in one transaction, and none of them grows with the number of
/// broadcasts: the caller's personal rows are flipped by one <c>ExecuteUpdate</c>; the caller's read cursor
/// is moved to one instant by a hand-written <c>INSERT ... ON CONFLICT</c>; and the caller's visit rows the
/// cursor now covers are deleted by one <c>ExecuteDelete</c>, the read ones because the cursor makes them
/// redundant and the unread ones because "mark all as read" means that everything up to now is read.
/// Together they cover exactly the set <c>INotificationService.GetUnreadCountAsync</c> counts, so the
/// unread badge reads zero afterwards.
/// </remarks>
sealed class NotificationMarkAllAsReadEndpoint(AppDbContext dbContext, ICurrentUserService currentUserService, INotificationPublisher publisher) : EndpointWithoutRequest<NotificationMarkAllAsReadResponse>
{
    public override void Configure()
    {
        Post("mark-all-as-read");
        Group<NotificationsGroup>();
    }

    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetCurrentUserId();
        if (userId == null)
        {
            await Send.UnauthorizedAsync(cancellationToken);
            return;
        }

        // One instant for every cursor and for the visits they cover, so what the cursors say is read and
        // what the delete removes are the same set. It is taken the way a notification's CreatedAt is.
        var readAllAt = DateTime.UtcNow;

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Through the same VisibleTo the list and unread-count reads use, narrowed to the caller's own
        // personal rows. The soft-delete filter stays in force.
        await dbContext.Notifications
            .VisibleTo(userId.Value)
            .Where(x => x.UserId == userId.Value && !x.IsRead)
            .ExecuteUpdateAsync(setters => setters.SetProperty(notification => notification.IsRead, true), cancellationToken);

        // Read state for a broadcast is the caller's read cursor. It is written by hand so two requests
        // racing cannot both insert one; GREATEST keeps it from moving backwards.
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO notifications."NotificationReadCursors" AS existing ("Id", "UserId", "ReadAllAt")
            VALUES (gen_random_uuid(), {userId.Value}, {readAllAt})
            ON CONFLICT ("UserId") DO UPDATE SET "ReadAllAt" = GREATEST(existing."ReadAllAt", EXCLUDED."ReadAllAt")
            """, cancellationToken);

        // The caller's visit rows on the audience notifications the cursors now cover are removed, read and
        // unread alike: a read one only repeats what the cursor says, and an unread one would contradict
        // "mark all as read".
        await dbContext.NotificationVisits
            .Where(visit => visit.UserId == userId.Value &&
                            visit.Notification.UserId == null &&
                            visit.Notification.CreatedAt <= readAllAt)
            .ExecuteDeleteAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        // Only the caller's own group is told: marking everything read changes nobody else's
        // count, so nothing is recomputed per member of any audience.
        await publisher.PublishUnreadCountAsync(userId.Value, cancellationToken);

        await Send.ResponseAsync(new NotificationMarkAllAsReadResponse { Success = true, Message = "All notifications marked as read" }, cancellation: cancellationToken);
    }
}

/// <summary>
/// Response payload confirming that all notifications were marked as read.
/// </summary>
public sealed class NotificationMarkAllAsReadResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = null!;
}
