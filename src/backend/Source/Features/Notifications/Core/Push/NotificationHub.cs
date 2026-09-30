namespace Backend.Features.Notifications.Core.Push;

using System.Security.Claims;
using Backend.Processors;
using Microsoft.AspNetCore.SignalR;

/// <summary>
/// The server-to-client hub new notifications and read-state changes are pushed through. It declares no
/// client-callable method: a client connects, is joined to the groups its session names, and listens.
/// </summary>
/// <remarks>
/// <para>
/// It is mapped at <see cref="Path"/> behind the same authentication as every HTTP endpoint, so the
/// principal here is the projected session - the claims <c>SessionClaims.Project</c> put on it after the
/// session store confirmed the session - and the groups are built from it alone: the account and the
/// tenant it acts in, never anything the client sends.
/// </para>
/// <para>
/// A connection over <see cref="NotificationOptions.MaxConnectionsPerUser"/> for its account on this
/// instance is refused by throwing a <see cref="HubException"/> from <see cref="OnConnectedAsync"/>, which
/// SignalR answers by closing the connection with that message before it joins any group.
/// </para>
/// </remarks>
/// <param name="registry">The connections open on this instance.</param>
/// <param name="options">The notifications options, for the per-account cap.</param>
/// <param name="logger">Logs a refused connection.</param>
public sealed class NotificationHub(
    NotificationConnectionRegistry registry,
    IOptions<NotificationOptions> options,
    ILogger<NotificationHub> logger) : Hub
{
    /// <summary>The path the hub is mapped at, outside the API's route prefix.</summary>
    public const string Path = "/hubs/notifications";

    /// <summary>
    /// The close reason a connection over the per-account cap is refused with.
    /// </summary>
    public const string ConnectionLimitExceeded = "connectionLimitExceeded";

    /// <summary>
    /// Records the connection and joins it to its groups: its account in its scope, its tenant when it acts
    /// in one, and the platform-wide group.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var principal = Context.User ?? new ClaimsPrincipal();

        // The hub is mapped with RequireAuthorization, so an unauthenticated connection never gets here;
        // this guards against a principal that authenticated without naming an account at all.
        if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            throw new HubException("unauthenticated");
        }

        // Read the same way the request pipeline reads it, so the scope a connection is grouped under is
        // the scope an HTTP request with the same credential would act in.
        var tenantId = TenantContextProcessor.ReadSessionTenantId(principal);
        var connection = new NotificationConnection(Context.ConnectionId, userId, tenantId, ReadSessionId(principal), Context);

        var maxPerUser = options.Value.MaxConnectionsPerUser;
        if (!registry.TryAdd(connection, maxPerUser))
        {
            logger.LogWarning(
                "Notification hub connection {ConnectionId} refused: account {UserId} already holds {MaxConnectionsPerUser} connections on this instance.",
                Context.ConnectionId, userId, maxPerUser);
            throw new HubException(ConnectionLimitExceeded);
        }

        try
        {
            // The account's own group is joined last, and that order is relied on: SignalR answers the
            // handshake before this method runs, so a client cannot tell from its start completing that it
            // has joined anything. A message to the u: group arriving is the proof that every join before it
            // has finished - which is how a test (and any client that needs to) knows the connection is ready.
            if (tenantId is { } activeTenantId)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, NotificationGroups.Tenant(activeTenantId), Context.ConnectionAborted);
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, NotificationGroups.All, Context.ConnectionAborted);
            await Groups.AddToGroupAsync(Context.ConnectionId, NotificationGroups.User(userId, tenantId), Context.ConnectionAborted);
        }
        catch
        {
            // OnDisconnectedAsync does not run for a connection whose OnConnectedAsync threw, so the place
            // it took is given back here.
            registry.Remove(Context.ConnectionId);
            throw;
        }

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Forgets the connection. SignalR removes it from its groups by itself.
    /// </summary>
    /// <param name="exception">Why the connection closed, if it closed with an error.</param>
    public override Task OnDisconnectedAsync(Exception? exception)
    {
        registry.Remove(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Reads the session identifier the credential carries. Inbound JWT claim mapping can surface the raw
    /// <c>sid</c> claim as <see cref="ClaimTypes.Sid"/>, so both spellings are accepted, as Identity's own
    /// reader does.
    /// </summary>
    private static string? ReadSessionId(ClaimsPrincipal principal)
    {
        var sessionId = principal.FindFirstValue("sid") ?? principal.FindFirstValue(ClaimTypes.Sid);
        return string.IsNullOrWhiteSpace(sessionId) ? null : sessionId;
    }
}
