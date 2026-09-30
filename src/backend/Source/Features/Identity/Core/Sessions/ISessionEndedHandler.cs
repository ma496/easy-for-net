namespace Backend.Features.Identity.Core.Sessions;

using Backend.Attributes;

/// <summary>
/// Reacts to sessions having ended - whatever ended them: a revocation, a sign-out, a refresh or a tenant
/// switch or exit replacing the session, or a refused issuance withdrawing it. This is how another slice
/// lets go of what it holds for a session (a notification hub connection authenticated by it, say) without
/// Identity knowing that slice exists.
/// </summary>
/// <remarks>
/// <para>
/// Register an implementation as a singleton (<c>services.AddSingleton&lt;ISessionEndedHandler, …&gt;()</c>);
/// several may be registered and each is told. It is called on <b>every API instance</b>, not only the one
/// that ended the session, so an implementation acts on what this process holds and nothing else.
/// </para>
/// <para>
/// It is called after the session record is gone from the session store, with identifiers only, and
/// possibly more than once for one session or for a session this process never saw - so it must be
/// idempotent and cheap, and must not block: it runs on the request that ended the session, or on the
/// thread delivering the cross-instance message. A handler that throws is logged and does not stop the
/// others, nor fail what ended the session.
/// </para>
/// </remarks>
[AllowOutside]
public interface ISessionEndedHandler
{
    /// <summary>Called once the sessions named have been deleted from the session store.</summary>
    /// <param name="sessionIds">The ended sessions' identifiers - the <c>sid</c> their tokens carried.</param>
    /// <param name="cancellationToken">Token used to cancel the reaction.</param>
    Task OnSessionsEndedAsync(IReadOnlyCollection<string> sessionIds, CancellationToken cancellationToken = default);
}
