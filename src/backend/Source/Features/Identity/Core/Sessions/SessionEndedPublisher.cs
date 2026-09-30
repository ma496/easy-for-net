namespace Backend.Features.Identity.Core.Sessions;

/// <summary>
/// Tells every API instance's <see cref="ISessionEndedHandler"/>s that sessions have ended. Private to
/// Identity: the session store's decorator (<see cref="PublishingSessionStore"/>) is its only caller, so
/// every deletion of a session record announces itself and no deletion point has to remember to.
/// </summary>
/// <remarks>
/// Publishing never throws. A handler that fails, or a transport that cannot be reached, is logged and
/// swallowed, because the session has already ended by the time this runs and whatever ended it must
/// still succeed.
/// </remarks>
public interface ISessionEndedPublisher
{
    /// <summary>Announces that the sessions named have been deleted from the store.</summary>
    /// <param name="sessionIds">The ended sessions; nothing is published for an empty collection.</param>
    Task PublishAsync(IReadOnlyCollection<string> sessionIds);
}

/// <summary>
/// Calls this process's <see cref="ISessionEndedHandler"/>s, each in isolation: one that throws is logged
/// and the rest are still called. Both publishers, and the cross-instance subscriber, deliver through it.
/// </summary>
/// <param name="handlers">Every handler registered in this process.</param>
/// <param name="logger">Logs a handler failure.</param>
public sealed class LocalSessionEndedDispatcher(IEnumerable<ISessionEndedHandler> handlers, ILogger<LocalSessionEndedDispatcher> logger)
{
    private readonly ISessionEndedHandler[] _handlers = [.. handlers];

    /// <summary>Tells every local handler that the sessions named have ended. Never throws.</summary>
    /// <param name="sessionIds">The ended sessions.</param>
    public async Task DispatchAsync(IReadOnlyCollection<string> sessionIds)
    {
        if (sessionIds.Count == 0)
        {
            return;
        }

        foreach (var handler in _handlers)
        {
            try
            {
                await handler.OnSessionsEndedAsync(sessionIds, CancellationToken.None);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Session-ended handler {Handler} failed for {SessionCount} session(s).",
                    handler.GetType().Name, sessionIds.Count);
            }
        }
    }
}

/// <summary>
/// The publisher of a host running a single process - the <c>Testing</c> host: it calls the local handlers
/// directly, since there is no other instance to tell.
/// </summary>
/// <param name="dispatcher">This process's handlers.</param>
[NoDirectUse]
public sealed class InProcessSessionEndedPublisher(LocalSessionEndedDispatcher dispatcher) : ISessionEndedPublisher
{
    /// <inheritdoc />
    public Task PublishAsync(IReadOnlyCollection<string> sessionIds) => dispatcher.DispatchAsync(sessionIds);
}

/// <summary>
/// An <see cref="ISessionStore"/> that announces, through <see cref="ISessionEndedPublisher"/>, every
/// session record it deletes - the single point every way of ending a session passes through, so a
/// revocation, a sign-out, a refresh or a tenant switch or exit reaches the handlers without any of them
/// having to call anything.
/// </summary>
/// <remarks>
/// The announcement follows a deletion that succeeded: a store that cannot be reached throws exactly as it
/// did, and announces nothing. <see cref="DeleteAsync"/> announces the session asked for whether or not
/// its record was still there - it has ended either way, and a handler holding anything for it should let
/// go. A failure to announce is logged and never fails the deletion.
/// </remarks>
/// <param name="inner">The store the records live in.</param>
/// <param name="publisher">Where ended sessions are announced.</param>
/// <param name="logger">Logs a failure to announce.</param>
[NoDirectUse]
public sealed class PublishingSessionStore(ISessionStore inner, ISessionEndedPublisher publisher, ILogger<PublishingSessionStore> logger) : ISessionStore
{
    /// <summary>Gets the store being wrapped.</summary>
    public ISessionStore Inner => inner;

    /// <inheritdoc />
    public Task CreateAsync(SessionRecord session, CancellationToken cancellationToken = default)
        => inner.CreateAsync(session, cancellationToken);

    /// <inheritdoc />
    public Task<SessionRecord?> GetAsync(string sessionId, CancellationToken cancellationToken = default)
        => inner.GetAsync(sessionId, cancellationToken);

    /// <inheritdoc />
    public async Task DeleteAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        await inner.DeleteAsync(sessionId, cancellationToken);
        await AnnounceAsync([sessionId]);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> RevokeByUserAsync(Guid userId, CancellationToken cancellationToken = default)
        => await AnnounceAsync(await inner.RevokeByUserAsync(userId, cancellationToken));

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> RevokeByUserInTenantAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default)
        => await AnnounceAsync(await inner.RevokeByUserInTenantAsync(userId, tenantId, cancellationToken));

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> RevokeByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => await AnnounceAsync(await inner.RevokeByTenantAsync(tenantId, cancellationToken));

    /// <summary>
    /// Publishes the ended sessions, swallowing any failure: the sessions are gone from the store already,
    /// and whatever ended them has succeeded. Not cancelled with the request, which may have been aborted
    /// between the deletion and this.
    /// </summary>
    private async Task<IReadOnlyList<string>> AnnounceAsync(IReadOnlyList<string> sessionIds)
    {
        if (sessionIds.Count == 0)
        {
            return sessionIds;
        }

        try
        {
            await publisher.PublishAsync(sessionIds);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Announcing {SessionCount} ended session(s) failed.", sessionIds.Count);
        }

        return sessionIds;
    }
}
