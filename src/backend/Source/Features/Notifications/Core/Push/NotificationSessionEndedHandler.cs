namespace Backend.Features.Notifications.Core.Push;

using Backend.Features.Identity.Core.Sessions;

/// <summary>
/// Closes this instance's hub connections whose credential named a session that has ended, so a signed-out,
/// revoked or replaced session stops receiving pushes at once rather than when its access token expires.
/// </summary>
/// <remarks>
/// Identity calls it on every API instance after the session records are deleted, so each instance closes
/// the connections it holds and no other. A session with no connection here is nothing to do.
/// </remarks>
/// <param name="registry">The connections open on this instance.</param>
/// <param name="logger">Logs each connection closed.</param>
public sealed class NotificationSessionEndedHandler(
    NotificationConnectionRegistry registry,
    ILogger<NotificationSessionEndedHandler> logger) : ISessionEndedHandler
{
    /// <inheritdoc />
    public Task OnSessionsEndedAsync(IReadOnlyCollection<string> sessionIds, CancellationToken cancellationToken = default)
    {
        foreach (var sessionId in sessionIds)
        {
            foreach (var connection in registry.OfSession(sessionId))
            {
                logger.LogInformation(
                    "Closing notification hub connection {ConnectionId} of account {UserId}: its session ended.",
                    connection.ConnectionId, connection.UserId);

                // Aborting closes the transport; OnDisconnectedAsync then forgets the connection.
                connection.Context.Abort();
            }
        }

        return Task.CompletedTask;
    }
}
