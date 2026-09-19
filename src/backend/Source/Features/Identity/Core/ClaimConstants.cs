namespace Backend.Features.Identity.Core;

/// <summary>
/// Centralized string constants for the claim type names used by the identity system.
/// </summary>
public static class ClaimConstants
{
    public const string Permission = "permission";
    public const string SessionVersion = "session_version";

    /// <summary>
    /// Claim type carrying the identifier of the tenant the session is currently acting in. A
    /// principal holds this claim at most once, and holds it only while a tenant is active: when the
    /// signed-in user has no active membership, or has more than one and has not chosen between them,
    /// the claim is absent and tenant-scoped work is refused until a tenant is selected.
    /// </summary>
    public const string TenantId = "tenant_id";

    /// <summary>
    /// Claim type marking the session as belonging to a platform account. It carries the account's
    /// tier and never a grant: what the session may do is decided by its permission claims, which are
    /// already narrowed to the scope it is acting in. It is present only for a platform account, is
    /// recomputed from current data on every request alongside the role and permission claims, and
    /// survives entering a tenant - a platform account inside a tenant is still a platform account,
    /// which is how it finds its way back out.
    /// </summary>
    public const string IsPlatform = "is_platform";
}
