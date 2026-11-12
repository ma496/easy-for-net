using Backend.ShareData.Entities.Base;

namespace Backend.Features.Identity.Core.Entities;

/// <summary>
/// Server-side record of an issued JWT access/refresh token pair, used to validate refresh requests and revoke tokens.
/// </summary>
public class AuthToken : CreatableEntity<Guid>
{
    public string AccessToken { get; set; } = null!;
    public DateTime AccessExpiry { get; set; }
    public string RefreshToken { get; set; } = null!;
    public DateTime RefreshExpiry { get; set; }

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

    /// <summary>
    /// The account's <see cref="User.SecurityStamp"/> when the chain this pair belongs to was signed in,
    /// carried forward on every renewal. Rows that predate the column hold <see cref="Guid.Empty"/> and
    /// stay refreshable until the account's first credential change or deactivation.
    /// </summary>
    public Guid SecurityStamp { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
}
