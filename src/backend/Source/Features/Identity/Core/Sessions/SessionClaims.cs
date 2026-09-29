namespace Backend.Features.Identity.Core.Sessions;

using System.Security.Claims;

/// <summary>
/// The one place a session becomes claims and a token's two claims are read back. A token or cookie
/// carries only the account and the session identifier; <see cref="Project"/> turns the stored session
/// into the role, permission, tenant, tier, name and email claims the rest of the API reads, and
/// nothing else in the code base writes them.
/// </summary>
public static class SessionClaims
{
    private static readonly HashSet<string> ProjectedTypes =
    [
        ClaimTypes.Role,
        ClaimTypes.Name,
        ClaimTypes.Email,
        ClaimConstants.Permission,
        ClaimConstants.TenantId,
        ClaimConstants.IsPlatform,
    ];

    /// <summary>
    /// The claims a token or auth cookie is minted with: who the caller is and which session it is.
    /// </summary>
    /// <param name="userId">The account the session belongs to.</param>
    /// <param name="sessionId">The session the token belongs to.</param>
    /// <returns>Exactly two claims.</returns>
    public static List<Claim> ForToken(Guid userId, string sessionId)
        =>
        [
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimConstants.SessionId, sessionId),
        ];

    /// <summary>
    /// Reads the session identifier off a principal. JWT inbound claim mapping can turn a raw
    /// <c>sid</c> into <see cref="ClaimTypes.Sid"/>, so both spellings are accepted.
    /// </summary>
    /// <param name="principal">The principal a handler authenticated.</param>
    /// <returns>The session identifier, or <see langword="null"/> when the principal names none.</returns>
    public static string? ReadSessionId(ClaimsPrincipal principal)
    {
        var sessionId = principal.FindFirst(ClaimConstants.SessionId)?.Value
                        ?? principal.FindFirst(ClaimTypes.Sid)?.Value;

        return string.IsNullOrWhiteSpace(sessionId) ? null : sessionId;
    }

    /// <summary>
    /// Reads the account identifier off a principal.
    /// </summary>
    /// <param name="principal">The principal a handler authenticated.</param>
    /// <returns>The account, or <see langword="null"/> when the principal names none.</returns>
    public static Guid? ReadUserId(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId) ? userId : null;

    /// <summary>
    /// Adds the claims the session grants to the request's in-memory identity. Nothing is written back
    /// to the token or the cookie: the projection is rebuilt from the store on every request.
    /// </summary>
    /// <param name="identity">The identity the token or cookie authenticated.</param>
    /// <param name="session">The stored session it named.</param>
    public static void Project(ClaimsIdentity identity, SessionRecord session)
    {
        // Whatever of these the token or cookie arrived carrying is dropped first, so the stored session is
        // the only source of them: a credential minted before this scheme, or forged into a cookie ticket
        // key, cannot add a role, a permission or a tenant the session does not hold.
        foreach (var claim in identity.Claims.Where(claim => ProjectedTypes.Contains(claim.Type)).ToList())
        {
            identity.RemoveClaim(claim);
        }

        identity.AddClaim(new Claim(ClaimTypes.Name, session.Username));
        identity.AddClaim(new Claim(ClaimTypes.Email, session.Email));
        if (session.IsPlatform)
        {
            identity.AddClaim(new Claim(ClaimConstants.IsPlatform, bool.TrueString));
        }

        if (session.TenantId is { } tenantId)
        {
            identity.AddClaim(new Claim(ClaimConstants.TenantId, tenantId.ToString()));
        }

        identity.AddClaims(session.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
        identity.AddClaims(session.Permissions.Select(permission => new Claim(ClaimConstants.Permission, permission)));
    }
}
