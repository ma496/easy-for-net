namespace Backend.Features.Notifications.Core;

using Backend.Features.Notifications.Core.Entities;

/// <summary>
/// The one definition of which notifications a caller can see in the scope they act in, so that every
/// surface answering about a notification - reading one, listing them, counting them unread, marking them
/// - answers about the same set.
/// </summary>
/// <remarks>
/// Each of those surfaces has to relax the tenant query filter by name and narrow back down by hand,
/// because the filter alone would hide every platform-wide notification from a caller acting in a tenant:
/// those name no tenant, and a filter comparing the row's tenant to the active one can never match them.
/// Leaving that relaxation to each surface to spell out is how one of them ends up hiding the
/// platform-wide notifications the list still shows, so it is spelled out once, here. The same holds for
/// whether a visible notification is read, which <see cref="WithReadState"/> decides for every surface.
/// </remarks>
internal static class NotificationQueries
{
    /// <summary>
    /// The notifications one user can see in the scope named: the notifications raised in that scope -
    /// the tenant, or platform scope - addressed to them personally or to every member of it, and the
    /// platform-wide notifications, which stay visible in every scope. A personal notification raised in
    /// another scope is not among them, so a platform-scope one does not follow its recipient into a
    /// tenant, and one raised in a tenant is seen nowhere else.
    /// </summary>
    /// <param name="notifications">The notifications source, with the tenant filter already relaxed by <c>AcrossAllTenants()</c>.</param>
    /// <param name="userId">Identifier of the user whose notifications are wanted.</param>
    /// <param name="activeTenantId">The tenant being acted in, or <see langword="null"/> for platform scope.</param>
    /// <returns>The query narrowed to the notifications visible to that user in that scope.</returns>
    internal static IQueryable<Notification> VisibleTo(this IQueryable<Notification> notifications, Guid userId, Guid? activeTenantId)
        => notifications.Where(notification =>
            (notification.TenantId == activeTenantId && (notification.UserId == userId || notification.UserId == null)) ||
            (notification.TenantId == null && notification.UserId == null));

    /// <summary>
    /// The most unread notifications <see cref="CountUnreadAsync"/> counts. The badge shows "99+" long before
    /// this, and stopping here keeps the count one bounded scan however much a user has left unread.
    /// </summary>
    internal const int UnreadCountCap = 100;

    /// <summary>
    /// Pairs each notification with whether the given user has read it. This is the one place the read rule
    /// is written, so the unread count, the list's read filter, its default ordering and its projection, the
    /// single read, and anything added later all answer the same question the same way.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A personal notification (one naming a user) carries its own read flag. An audience notification -
    /// tenant-wide or platform-wide - is shared by all its readers, so its state is the user's: a
    /// <see cref="NotificationVisit"/> row for (notification, user), when there is one, decides it either
    /// way whatever its age; otherwise it is read when the user's <see cref="NotificationReadCursor"/> for
    /// the notification's audience - the cursor naming the same tenant, or the platform-wide cursor (no
    /// tenant) for a platform-wide notification - was set at or after the notification was created.
    /// </para>
    /// <para>
    /// The cursor comparison <c>cursor.TenantId == notification.TenantId</c> relies on EF's C# null
    /// semantics, which translate an equality of two nullable columns so that null matches null; that is
    /// what pairs a platform-wide notification with the platform-wide cursor. The cursor source is read
    /// across tenants because the platform-wide cursor names no tenant and the tenant filter alone would
    /// hide it from a caller acting in a tenant; the explicit user and tenant predicates narrow it back down.
    /// Apply this after <see cref="VisibleTo"/>, never instead of it.
    /// </para>
    /// </remarks>
    /// <param name="notifications">The notifications, already narrowed by <see cref="VisibleTo"/>.</param>
    /// <param name="dbContext">The context the query runs in, for the visit and cursor subqueries.</param>
    /// <param name="userId">Identifier of the user whose read state is wanted.</param>
    /// <returns>Each notification with the user's read state beside it.</returns>
    internal static IQueryable<NotificationWithReadState> WithReadState(this IQueryable<Notification> notifications, AppDbContext dbContext, Guid userId)
        => notifications.Select(notification => new NotificationWithReadState
        {
            Notification = notification,
            IsRead = notification.UserId != null
                ? notification.IsRead
                : dbContext.NotificationVisits
                      .Where(visit => visit.NotificationId == notification.Id && visit.UserId == userId)
                      .Select(visit => (bool?)visit.IsRead)
                      .FirstOrDefault()
                  ?? dbContext.NotificationReadCursors
                      .AcrossAllTenants()
                      .Any(cursor => cursor.UserId == userId &&
                                     cursor.TenantId == notification.TenantId &&
                                     notification.CreatedAt <= cursor.ReadAllAt)
        });

    /// <summary>
    /// Counts the notifications one user has not read in the scope named, stopping at
    /// <see cref="UnreadCountCap"/>. It is a single statement - a count over a limited subquery - and never
    /// materializes the identifiers of what it counts, so it can be asked for any user and scope, a
    /// background job's included, as cheaply as for the caller of a request.
    /// </summary>
    /// <param name="dbContext">The context to read through.</param>
    /// <param name="userId">Identifier of the user whose unread notifications are counted.</param>
    /// <param name="activeTenantId">The tenant the user acts in, or <see langword="null"/> for platform scope.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>The number of unread notifications, at most <see cref="UnreadCountCap"/>.</returns>
    internal static Task<int> CountUnreadAsync(AppDbContext dbContext, Guid userId, Guid? activeTenantId, CancellationToken cancellationToken)
        => UnreadQuery(dbContext, userId, activeTenantId).CountAsync(cancellationToken);

    /// <summary>
    /// The query <see cref="CountUnreadAsync"/> counts, kept apart so the statement it becomes can be inspected.
    /// </summary>
    /// <param name="dbContext">The context to read through.</param>
    /// <param name="userId">Identifier of the user whose unread notifications are counted.</param>
    /// <param name="activeTenantId">The tenant the user acts in, or <see langword="null"/> for platform scope.</param>
    /// <returns>The user's unread notifications in that scope, limited to <see cref="UnreadCountCap"/> rows.</returns>
    internal static IQueryable<NotificationWithReadState> UnreadQuery(AppDbContext dbContext, Guid userId, Guid? activeTenantId)
        => dbContext.Notifications
            .AsNoTracking()
            .AcrossAllTenants()
            .VisibleTo(userId, activeTenantId)
            .WithReadState(dbContext, userId)
            .Where(row => !row.IsRead)
            .Take(UnreadCountCap);

    /// <summary>
    /// Records one user's explicit read state for an audience notification, inserting the visit row or
    /// updating the one already there. It is a single <c>INSERT ... ON CONFLICT</c> on the unique
    /// (NotificationId, UserId) index, so two concurrent marks cannot both insert and whichever lands last wins.
    /// </summary>
    /// <remarks>
    /// The caller must already have found the notification through <see cref="VisibleTo"/> and seen that it
    /// is an audience one: no query filter reaches a hand-written statement, and the visit table carries no
    /// tenant of its own, so that lookup is the tenant restriction. Writing <see langword="false"/> is what
    /// lets a notification the user's read cursor covers be put back among the unread ones.
    /// </remarks>
    /// <param name="dbContext">The context to write through.</param>
    /// <param name="notificationId">The audience notification.</param>
    /// <param name="userId">The user whose read state is recorded.</param>
    /// <param name="isRead">Whether the notification is read for that user.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>The number of rows written.</returns>
    internal static Task<int> SetAudienceReadStateAsync(AppDbContext dbContext, Guid notificationId, Guid userId, bool isRead, CancellationToken cancellationToken)
        => dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO notifications."NotificationVisits" ("Id", "UserId", "VisitedAt", "IsRead", "NotificationId")
            VALUES (gen_random_uuid(), {userId}, NOW(), {isRead}, {notificationId})
            ON CONFLICT ("NotificationId", "UserId") DO UPDATE SET "IsRead" = EXCLUDED."IsRead", "VisitedAt" = EXCLUDED."VisitedAt"
            """, cancellationToken);
}

/// <summary>
/// A notification together with whether one user has read it, as <see cref="NotificationQueries.WithReadState"/>
/// resolves it. Filtering, ordering and projecting on <see cref="IsRead"/> is how a surface uses the read rule
/// without restating it.
/// </summary>
internal sealed class NotificationWithReadState
{
    /// <summary>The notification.</summary>
    public Notification Notification { get; init; } = null!;

    /// <summary>Whether the user the query was built for has read it.</summary>
    public bool IsRead { get; init; }
}
