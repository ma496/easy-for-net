namespace Backend.Features.Notifications.Endpoints.Notifications;

using Backend.Features.Identity.Core;
using Backend.Features.Notifications.Core;
using Backend.Features.Tenancy.Core;

/// <summary>
/// POST endpoint that marks every notification the current user can see in the active scope as read: their
/// own unread notifications raised in that scope, the notifications addressed to the whole tenant, and the
/// platform-wide ones. Personal notifications raised in another scope are left unread.
/// </summary>
/// <remarks>
/// <para>
/// The work is three bulk statements in one transaction, and none of them grows with the number of audience
/// notifications: the caller's personal rows are flipped by one <c>ExecuteUpdate</c>; the caller's read
/// cursors - for the active tenant's tenant-wide notifications when acting in a tenant, and for the
/// platform-wide ones always - are moved to one instant by hand-written <c>INSERT ... ON CONFLICT</c>
/// statements; and the caller's visit rows those cursors now cover are deleted by one <c>ExecuteDelete</c>,
/// the read ones because the cursor makes them redundant and the unread ones because "mark all as read"
/// means that everything up to now is read.
/// </para>
/// <para>
/// Each statement carries its tenant restriction in a predicate of its own. The cursor statements are reached
/// by no query filter at all, and the other two deliberately relax the tenant filter so that the platform-wide
/// rows stay reachable, which leaves the written predicates as the only thing keeping another tenant's rows
/// out. Together they cover exactly the set <c>INotificationService.GetUnreadCountAsync</c> counts, so the
/// unread badge reads zero afterwards. The platform-wide cursor names no tenant, so platform-wide
/// notifications marked read here read as read in every scope the caller acts in.
/// </para>
/// </remarks>
sealed class NotificationMarkAllAsReadEndpoint(AppDbContext dbContext, ICurrentUserService currentUserService, ITenantContext tenantContext) : EndpointWithoutRequest<NotificationMarkAllAsReadResponse>
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

        // Reading the active tenant here rather than leaning on the query filter is what lets the
        // platform-wide notifications back in: the filter alone would hide every row naming no tenant.
        // With no scope established this throws instead of marking rows the caller is not acting for.
        var activeTenantId = tenantContext.CurrentTenantId;

        // One instant for every cursor and for the visits they cover, so what the cursors say is read and
        // what the delete removes are the same set. It is taken the way a notification's CreatedAt is.
        var readAllAt = DateTime.UtcNow;

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Tenant restriction is relaxed by name and narrowed straight back down through the same
        // VisibleTo the list and unread-count reads use, so the rows this bulk update touches are the
        // caller's own notifications raised in the active scope and nothing else. The soft-delete filter
        // stays in force.
        await dbContext.Notifications
            .AcrossAllTenants()
            .VisibleTo(userId.Value, activeTenantId)
            .Where(x => x.UserId == userId.Value && !x.IsRead)
            .ExecuteUpdateAsync(setters => setters.SetProperty(notification => notification.IsRead, true), cancellationToken);

        // Read state for a notification addressed to an audience is the caller's read cursor for that
        // audience. The cursors are written by hand rather than through the change tracker because a
        // cursor is tenant-attributed at save time, and the platform-wide one - naming no tenant - would be
        // refused or stamped with the active tenant when saved while acting in one. No query filter reaches
        // these statements, which is why the tenant each names is spelled out: the active tenant, which
        // comes from the session and never from the request, and no tenant at all. GREATEST keeps a cursor
        // from moving backwards when two requests race.
        if (activeTenantId is { } tenantId)
        {
            await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO notifications."NotificationReadCursors" AS existing ("Id", "UserId", "TenantId", "ReadAllAt")
                VALUES (gen_random_uuid(), {userId.Value}, {tenantId}, {readAllAt})
                ON CONFLICT ("UserId", "TenantId") DO UPDATE SET "ReadAllAt" = GREATEST(existing."ReadAllAt", EXCLUDED."ReadAllAt")
                """, cancellationToken);
        }

        // The platform-wide cursor's tenant is written as a literal NULL: a parameter carrying no value
        // cannot be typed for the column, and the unique index treats nulls as equal, so the conflict
        // clause finds the caller's one platform-wide cursor.
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO notifications."NotificationReadCursors" AS existing ("Id", "UserId", "TenantId", "ReadAllAt")
            VALUES (gen_random_uuid(), {userId.Value}, NULL, {readAllAt})
            ON CONFLICT ("UserId", "TenantId") DO UPDATE SET "ReadAllAt" = GREATEST(existing."ReadAllAt", EXCLUDED."ReadAllAt")
            """, cancellationToken);

        // The caller's visit rows on the audience notifications the cursors now cover are removed, read and
        // unread alike: a read one only repeats what the cursor says, and an unread one would contradict
        // "mark all as read". The join to the notification brings its tenant filter with it, so that is
        // relaxed by name and the covered audiences - the active tenant's and the platform-wide - are named
        // explicitly; with platform scope active, the tenant comparison matches the platform-wide rows only.
        await dbContext.NotificationVisits
            .AcrossAllTenants()
            .Where(visit => visit.UserId == userId.Value &&
                            visit.Notification.UserId == null &&
                            (visit.Notification.TenantId == activeTenantId || visit.Notification.TenantId == null) &&
                            visit.Notification.CreatedAt <= readAllAt)
            .ExecuteDeleteAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

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
