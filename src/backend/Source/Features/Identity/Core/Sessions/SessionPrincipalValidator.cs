namespace Backend.Features.Identity.Core.Sessions;

using System.Security.Claims;
using Backend.Middleware;

/// <summary>
/// Decides, for the principal a token or cookie authenticated, whether the session it names is still
/// alive - and if it is, makes the session's grants the principal's claims. It runs on every
/// authenticated request, between authentication and authorization.
/// </summary>
public interface ISessionPrincipalValidator
{
    /// <summary>
    /// Looks the principal's session up and, when it stands, projects it onto the principal.
    /// </summary>
    /// <param name="httpContext">The request being authenticated.</param>
    /// <param name="principal">The principal the token or cookie produced; its identity is extended in memory.</param>
    /// <returns>
    /// <see langword="true"/> when the session stands and has been projected; <see langword="false"/>
    /// when authentication must fail - because the session is gone, or because the store could not be
    /// asked, which is recorded on the request so the pipeline answers 503 instead of treating the
    /// caller as anonymous.
    /// </returns>
    Task<bool> ValidateAsync(HttpContext httpContext, ClaimsPrincipal principal);
}

/// <summary>
/// Default <see cref="ISessionPrincipalValidator"/>. A session that is missing, expired or belongs to
/// another account than the token names fails authentication; a store that cannot answer fails it
/// closed and flags the outage. The projection it performs is the only place a stored session becomes
/// claims.
/// </summary>
[NoDirectUse]
public sealed class SessionPrincipalValidator(ISessionStore sessionStore, ILogger<SessionPrincipalValidator> logger) : ISessionPrincipalValidator
{
    /// <inheritdoc />
    public async Task<bool> ValidateAsync(HttpContext httpContext, ClaimsPrincipal principal)
    {
        if (SessionClaims.ReadSessionId(principal) is not { } sessionId
            || SessionClaims.ReadUserId(principal) is not { } userId
            || principal.Identity is not ClaimsIdentity identity)
        {
            return false;
        }

        SessionRecord? session;
        try
        {
            session = await sessionStore.GetAsync(sessionId, httpContext.RequestAborted);
        }
        catch (SessionStoreUnavailableException exception)
        {
            logger.LogError(exception, "The session store is unavailable; refusing {Method} {Path}.",
                            httpContext.Request.Method, httpContext.Request.Path);
            httpContext.Items[SessionStoreUnavailableMiddleware.OutageItemKey] = true;
            return false;
        }

        // A session that names another account than the token does is not this caller's session, however
        // it came to be presented.
        if (session is null || session.UserId != userId)
        {
            return false;
        }

        SessionClaims.Project(identity, session);
        return true;
    }
}
