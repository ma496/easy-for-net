namespace Backend.Features.Notifications.Core;

/// <summary>
/// Maintenance service run daily as a recurring job: removes notifications past their retention period and
/// the visit rows a read cursor already makes redundant.
/// </summary>
public interface INotificationRetentionService
{
    /// <summary>
    /// Hard-deletes every notification older than <see cref="NotificationOptions.RetentionDays"/> (their visits
    /// cascade), then deletes the read visit rows that the visiting user's read cursor already covers.
    /// </summary>
    /// <param name="cancellationToken">Stops the work between batches; Hangfire supplies its shutdown token.</param>
    Task DeleteExpiredAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Default <see cref="INotificationRetentionService"/> implementation, written as batched hand-written
/// <c>DELETE</c> statements so that neither pass holds a long lock or one huge transaction however many rows
/// have piled up: each statement removes at most <see cref="BatchSize"/> rows and commits on its own, and the
/// pass repeats until a statement removes fewer.
/// </summary>
/// <remarks>
/// <para>
/// The service establishes no tenant scope, by design: it runs as a recurring job rather than on a request, so
/// there is no scope to inherit, and an expired notification or a redundant visit is rubbish whichever tenant
/// it belongs to. The statements are hand-written and carry no tenant predicate, so they span every tenant and
/// platform scope alike, and no query filter reaches them - which is also what lets the retention pass remove
/// soft-deleted notifications, which the soft-delete filter would otherwise hide.
/// </para>
/// <para>
/// It deletes nothing else: notifications whose <c>CreatedAt</c> is before the retention cutoff (their visit
/// rows go with them through the cascading foreign key), and visit rows with <c>IsRead = true</c> on an audience
/// notification the visiting user's read cursor for that notification's audience already covers. A visit with
/// <c>IsRead = false</c> is kept, because it overrides the cursor and marks the notification unread; a read
/// visit on a notification newer than the cursor is kept, because it is the only thing marking it read.
/// </para>
/// </remarks>
public class NotificationRetentionService(AppDbContext dbContext, IOptions<NotificationOptions> options) : INotificationRetentionService
{
    /// <summary>The most rows a single delete statement removes.</summary>
    public const int BatchSize = 5_000;

    public async Task DeleteExpiredAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-options.Value.RetentionDays);

        await DeleteInBatchesAsync(() => dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            DELETE FROM notifications."Notifications"
            WHERE "Id" IN (
                SELECT "Id" FROM notifications."Notifications"
                WHERE "CreatedAt" < {cutoff}
                LIMIT {BatchSize})
            """, cancellationToken), cancellationToken);

        // A cursor covers an audience notification - one with no UserId - when it names the notification's
        // audience (the same tenant, or both null for the platform-wide one) and was set at or after the
        // notification was created. A read visit under it repeats what the cursor already says.
        // "IsRead" is repeated on the delete target on purpose: when a concurrent mark-as-unread flips a visit
        // the subquery picked, PostgreSQL re-checks only the target's own predicates against the updated row,
        // so without it the now-unread visit would be deleted and the cursor would read it as read again.
        await DeleteInBatchesAsync(() => dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            DELETE FROM notifications."NotificationVisits"
            WHERE "IsRead"
              AND "Id" IN (
                SELECT visit."Id"
                FROM notifications."NotificationVisits" AS visit
                JOIN notifications."Notifications" AS notification ON notification."Id" = visit."NotificationId"
                JOIN notifications."NotificationReadCursors" AS read_cursor
                    ON read_cursor."UserId" = visit."UserId"
                   AND read_cursor."TenantId" IS NOT DISTINCT FROM notification."TenantId"
                WHERE visit."IsRead"
                  AND notification."UserId" IS NULL
                  AND notification."CreatedAt" <= read_cursor."ReadAllAt"
                LIMIT {BatchSize})
            """, cancellationToken), cancellationToken);
    }

    private static async Task DeleteInBatchesAsync(Func<Task<int>> deleteBatch, CancellationToken cancellationToken)
    {
        int deleted;
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            deleted = await deleteBatch();
        }
        while (deleted >= BatchSize);
    }
}
