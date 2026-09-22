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

    // The tenant this session is acting in, recorded when the session is established or switched.
    // Null means the session carries no active tenant - the state a user holding memberships in more
    // than one tenant is in until they select one, and the state of a session with no tenant at all.
    // Deliberately not IMayHaveTenant: the row is read during a refresh, before any tenant is
    // established, so a tenant query filter would hide it exactly when it is needed.
    public Guid? TenantId { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
}
