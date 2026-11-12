using Backend.Features.Notifications.Core.Entities;
using Backend.Features.Notifications.Core.Push;

namespace Backend.Features.Notifications.Core;

/// <summary>
/// Application service for creating and querying notifications. A notification is addressed to a single
/// account or to every user, and each addressing mode has its own method here.
/// </summary>
/// <remarks>
/// Published to other slices so that they can raise notifications about their own events; the entities
/// behind it stay private to this slice.
/// </remarks>
[AllowOutside]
public interface INotificationService
{
    /// <summary>
    /// Returns the number of unread notifications visible to the given user, counting no further than 100:
    /// their own unread notifications and the all-users broadcasts they have not read.
    /// </summary>
    /// <param name="userId">Identifier of the user whose unread notifications are counted.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>The number of unread notifications for the user, at most 100.</returns>
    Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a notification addressed to a single account.
    /// </summary>
    /// <param name="userId">Identifier of the recipient user.</param>
    /// <param name="type">Visual/severity category of the notification.</param>
    /// <param name="titleKey">Localization key used to render the notification title.</param>
    /// <param name="messageKey">Localization key used to render the notification body.</param>
    /// <param name="group">Optional logical grouping used to filter notifications in the UI.</param>
    /// <param name="metadata">Optional opaque metadata payload (typically JSON).</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    Task NewUserNotificationAsync(Guid userId, NotificationType type, string titleKey, string messageKey, string? group = null, string? metadata = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates one personal notification per account named, exactly as <see cref="NewUserNotificationAsync"/>
    /// would for each of them - but written as batched inserts of at most 1,000 rows each, inside one
    /// transaction (the caller's, when one is open), and pushed once per recipient. Use it rather than a
    /// loop over <see cref="NewUserNotificationAsync"/> when telling many accounts the same thing.
    /// </summary>
    /// <param name="userIds">The recipients; duplicates are addressed once, and an empty collection does nothing.</param>
    /// <param name="type">Visual/severity category of the notification.</param>
    /// <param name="titleKey">Localization key used to render the notification title.</param>
    /// <param name="messageKey">Localization key used to render the notification body.</param>
    /// <param name="group">Optional logical grouping used to filter notifications in the UI.</param>
    /// <param name="metadata">Optional opaque metadata payload (typically JSON).</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    Task NewUserNotificationsAsync(IReadOnlyCollection<Guid> userIds, NotificationType type, string titleKey, string messageKey, string? group = null, string? metadata = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a notification addressed to every user. Read state for it is per user and is tracked
    /// through notification visits and read cursors rather than through the row's read flag.
    /// </summary>
    /// <param name="type">Visual/severity category of the notification.</param>
    /// <param name="titleKey">Localization key used to render the notification title.</param>
    /// <param name="messageKey">Localization key used to render the notification body.</param>
    /// <param name="group">Optional logical grouping used to filter notifications in the UI.</param>
    /// <param name="metadata">Optional opaque metadata payload (typically JSON).</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    Task NewAllUsersNotificationAsync(NotificationType type, string titleKey, string messageKey, string? group = null, string? metadata = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default EF Core-backed implementation of <see cref="INotificationService"/>.
/// </summary>
/// <remarks>
/// Every committed notification is pushed to the hub group of the audience it was addressed to - one
/// message per notification, whatever the audience's size - through <see cref="INotificationPublisher"/>,
/// which waits for the caller's transaction to commit when one is open and never lets a failed push fail
/// the raise.
/// </remarks>
public class NotificationService(AppDbContext dbContext, INotificationPublisher publisher) : INotificationService
{
    /// <summary>
    /// The most rows <see cref="NewUserNotificationsAsync"/> inserts in one batch.
    /// </summary>
    internal const int BatchSize = 1000;

    /// <summary>
    /// Counts the unread notifications the user can see: the set <see cref="NotificationQueries.VisibleTo"/>
    /// defines, each row unread by the one rule <see cref="NotificationQueries.WithReadState"/> writes. The
    /// count is one statement that stops at <see cref="NotificationQueries.UnreadCountCap"/>.
    /// </summary>
    public Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default)
        => NotificationQueries.CountUnreadAsync(dbContext, userId, cancellationToken);

    /// <summary>
    /// Persists a notification addressed to one account and pushes it to that account's connections.
    /// </summary>
    public async Task NewUserNotificationAsync(Guid userId, NotificationType type, string titleKey, string messageKey, string? group = null, string? metadata = null, CancellationToken cancellationToken = default)
    {
        var notification = new Notification
        {
            UserId = userId,
            Type = type,
            TitleKey = titleKey,
            MessageKey = messageKey,
            Group = group,
            Metadata = metadata
        };

        dbContext.Notifications.Add(notification);
        await dbContext.SaveChangesAsync(cancellationToken);

        await PublishAsync(NotificationGroups.User(userId), notification);
    }

    /// <summary>
    /// Persists one personal notification per recipient, in batches of at most <see cref="BatchSize"/>,
    /// and pushes each to its recipient's group.
    /// </summary>
    public async Task NewUserNotificationsAsync(IReadOnlyCollection<Guid> userIds, NotificationType type, string titleKey, string messageKey, string? group = null, string? metadata = null, CancellationToken cancellationToken = default)
    {
        var recipients = userIds.Distinct().ToList();
        if (recipients.Count == 0)
        {
            return;
        }

        // One transaction for every batch, so the recipients get all of it or none of it, and the pushes -
        // deferred by the publisher while it is open - go out only once the last batch is committed. A
        // caller's own transaction is joined rather than nested; its commit then sends them.
        await using var ownTransaction = dbContext.Database.CurrentTransaction is null
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        foreach (var batch in recipients.Chunk(BatchSize))
        {
            var notifications = batch
                .Select(userId => new Notification
                {
                    UserId = userId,
                    Type = type,
                    TitleKey = titleKey,
                    MessageKey = messageKey,
                    Group = group,
                    Metadata = metadata
                })
                .ToList();

            // One SaveChanges per batch is one round trip of batched INSERT statements.
            dbContext.Notifications.AddRange(notifications);
            await dbContext.SaveChangesAsync(cancellationToken);

            foreach (var notification in notifications)
            {
                await PublishAsync(NotificationGroups.User(notification.UserId!.Value), notification);

                // Detached one by one rather than by clearing the tracker, which would also drop whatever the
                // caller is tracking; left attached, a large audience would grow the tracker batch by batch.
                dbContext.Entry(notification).State = EntityState.Detached;
            }
        }

        if (ownTransaction is not null)
        {
            await ownTransaction.CommitAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Persists a notification addressed to every user and pushes it to the all-users group.
    /// </summary>
    public async Task NewAllUsersNotificationAsync(NotificationType type, string titleKey, string messageKey, string? group = null, string? metadata = null, CancellationToken cancellationToken = default)
    {
        var notification = new Notification
        {
            UserId = null,
            Type = type,
            TitleKey = titleKey,
            MessageKey = messageKey,
            Group = group,
            Metadata = metadata
        };

        dbContext.Notifications.Add(notification);
        await dbContext.SaveChangesAsync(cancellationToken);

        await PublishAsync(NotificationGroups.All, notification);
    }

    /// <summary>
    /// Pushes a saved notification to a group, after the caller's transaction commits when one is open.
    /// </summary>
    /// <param name="group">The group of the audience it was addressed to.</param>
    /// <param name="notification">The saved notification.</param>
    private Task PublishAsync(string group, Notification notification)
        => publisher.PublishAsync(group, NotificationHubMethods.NotificationReceived, NotificationReceivedMessage.From(notification));
}
