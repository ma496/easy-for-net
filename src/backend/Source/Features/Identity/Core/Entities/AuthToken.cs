namespace Backend.Features.Identity.Core.Entities;

using Backend.ShareData.Entities.Base;

/// <summary>
/// Server-side record of an issued JWT access/refresh token pair, used to validate refresh requests and revoke tokens.
/// The row also carries the <see cref="TenantId"/> the session was established for, so that refreshing a session re-issues
/// the very same tenant instead of dropping it or carrying a previously active one.
/// </summary>
public class AuthToken : CreatableEntity<Guid>, IMayHaveTenant
{
    public string AccessToken { get; set; } = null!;
    public DateTime AccessExpiry { get; set; }
    public string RefreshToken { get; set; } = null!;
    public DateTime RefreshExpiry { get; set; }

    /// <summary>
    /// The tenant this session is acting in, recorded when the session is established or switched.
    /// Null is platform scope - a session that acts in no tenant, which is the state of a platform
    /// account and of an account that holds no active membership.
    /// </summary>
    /// <remarks>
    /// The marker makes these rows tenant-scoped like any other, but a session record is unusual in
    /// that the code touching it is the very code that changes the scope, so it neither reads nor
    /// writes from inside the tenant the row names. <see cref="AuthTokenService"/> therefore reads
    /// across tenants by name and opens the scope of the session's own tenant before writing the row,
    /// rather than inheriting whichever tenant the request happened to be acting in: a sign-in or a
    /// refresh is anonymous and acts in no scope at all, and a switch issues the session for the
    /// tenant being entered while still acting in the one being left.
    /// </remarks>
    public Guid? TenantId { get; set; }

    /// <summary>
    /// The id of the session this token pair belongs to - the <c>sid</c> claim carried by both tokens,
    /// an opaque string minted by the session store when the session is established.
    /// Null for rows that predate the column.
    /// </summary>
    /// <remarks>
    /// Rows are matched to their session so that revoking a session (or every session of a user but the
    /// current one) can also delete the refresh row, leaving no refresh token able to renew it.
    /// </remarks>
    public string? SessionId { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
}
