namespace Backend.Features.Identity.Core;

/// <summary>
/// Centralized string constants for the claim type names used by the identity system.
/// </summary>
public static class ClaimConstants
{
    public const string Permission = "permission";

    /// <summary>
    /// Claim type carrying the identifier of the session a token belongs to. It is, with the account's
    /// own identifier, the whole of what a token or auth cookie says: everything else about the
    /// session is read from the session store on every request and projected onto the request's
    /// principal. Inbound claim mapping may surface it as
    /// <see cref="System.Security.Claims.ClaimTypes.Sid"/>, so read it through
    /// <see cref="Sessions.SessionClaims.ReadSessionId"/>.
    /// </summary>
    public const string SessionId = "sid";
}
