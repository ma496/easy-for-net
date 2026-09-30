namespace Backend.Features.Notifications.Core.Push;

/// <summary>
/// Publishes notification events to the hub groups that may see them, once the change behind them is
/// committed, and never lets a failed push fail the change.
/// </summary>
/// <remarks>
/// Private to the notifications slice: other slices raise notifications through
/// <see cref="INotificationService"/>, which publishes for them.
/// </remarks>
public interface INotificationPublisher
{
    /// <summary>
    /// Sends a message to a group: at once when the context has no open transaction, otherwise when that
    /// transaction commits - and never, when it rolls back. A send that fails is logged and swallowed.
    /// </summary>
    /// <param name="group">The group, from <see cref="NotificationGroups"/>.</param>
    /// <param name="method">The client method, from <see cref="NotificationHubMethods"/>.</param>
    /// <param name="message">The payload.</param>
    Task PublishAsync(string group, string method, object message);

    /// <summary>
    /// Recomputes one user's capped unread count in one scope and publishes it as
    /// <see cref="NotificationHubMethods.UnreadCountChanged"/> to that user's group in that scope alone.
    /// A failure to count or to send is logged and swallowed: the change it reports has already been made.
    /// </summary>
    /// <param name="userId">The user whose read state changed.</param>
    /// <param name="tenantId">The scope they changed it in, or <see langword="null"/> for platform scope.</param>
    /// <param name="cancellationToken">Token used to cancel the count.</param>
    Task PublishUnreadCountAsync(Guid userId, Guid? tenantId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default <see cref="INotificationPublisher"/>. It reads the open transaction off the same scoped
/// <see cref="AppDbContext"/> the notification was saved through, and hands a deferred send to
/// <see cref="NotificationCommitInterceptor"/>, which runs it on that transaction's commit.
/// </summary>
/// <param name="dbContext">The context the change was made through.</param>
/// <param name="commitInterceptor">Holds sends until their transaction commits.</param>
/// <param name="sender">Sends to the hub.</param>
/// <param name="logger">Logs a failed push.</param>
[NoDirectUse]
public sealed class NotificationPublisher(
    AppDbContext dbContext,
    NotificationCommitInterceptor commitInterceptor,
    INotificationHubSender sender,
    ILogger<NotificationPublisher> logger) : INotificationPublisher
{
    /// <inheritdoc />
    public async Task PublishAsync(string group, string method, object message)
    {
        // The send is built to reach nothing scoped - the singleton sender, the logger and the payload -
        // because a deferred one runs from the commit hook, possibly after this scope has moved on. The
        // request's cancellation token is deliberately not passed: once the row is committed, its push
        // is owed whether or not the caller is still waiting for the response.
        Func<Task> send = () => SendSafelyAsync(group, method, message);

        if (dbContext.Database.CurrentTransaction is { } transaction)
        {
            commitInterceptor.Defer(dbContext, transaction.TransactionId, send);
            return;
        }

        await send();
    }

    /// <inheritdoc />
    public async Task PublishUnreadCountAsync(Guid userId, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        int count;
        try
        {
            // The count is NotificationQueries.CountUnreadAsync, the one computation of it, taken now - inside
            // the caller's transaction, when there is one, so it sees the change that transaction is about to
            // commit.
            count = await NotificationQueries.CountUnreadAsync(dbContext, userId, tenantId, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "The unread count could not be recomputed for a push; the change it reports stands.");
            return;
        }

        await PublishAsync(NotificationGroups.User(userId, tenantId), NotificationHubMethods.UnreadCountChanged, new UnreadCountChangedMessage { Count = count });
    }

    /// <summary>
    /// Sends, logging a failure instead of throwing it.
    /// </summary>
    private async Task SendSafelyAsync(string group, string method, object message)
    {
        try
        {
            await sender.SendAsync(group, method, message);
        }
        catch (Exception exception)
        {
            // The group names only identifiers, never a credential or a notification's content.
            logger.LogWarning(exception, "Notification push {Method} to group {Group} failed.", method, group);
        }
    }
}
