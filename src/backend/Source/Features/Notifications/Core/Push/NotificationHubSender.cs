namespace Backend.Features.Notifications.Core.Push;

using Microsoft.AspNetCore.SignalR;

/// <summary>
/// Sends one message to one hub group. It is the only thing that talks to SignalR, so it is the seam a test
/// replaces to make a push fail, and the thing <see cref="INotificationPublisher"/> calls once a message is
/// safe to send.
/// </summary>
public interface INotificationHubSender
{
    /// <summary>
    /// Invokes a client method on every connection in a group - on every API instance, when the Redis
    /// backplane is configured.
    /// </summary>
    /// <param name="group">The group, from <see cref="NotificationGroups"/>.</param>
    /// <param name="method">The client method, from <see cref="NotificationHubMethods"/>.</param>
    /// <param name="message">The payload.</param>
    /// <param name="cancellationToken">Token used to cancel the send.</param>
    Task SendAsync(string group, string method, object message, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default <see cref="INotificationHubSender"/>, over the hub context. It is a singleton and holds no scoped
/// state, which is what lets a send deferred until a transaction commits run whenever that happens.
/// </summary>
/// <param name="hubContext">The notification hub's context.</param>
[NoDirectUse]
public sealed class NotificationHubSender(IHubContext<NotificationHub> hubContext) : INotificationHubSender
{
    /// <inheritdoc />
    public Task SendAsync(string group, string method, object message, CancellationToken cancellationToken = default)
        => hubContext.Clients.Group(group).SendAsync(method, message, cancellationToken);
}
