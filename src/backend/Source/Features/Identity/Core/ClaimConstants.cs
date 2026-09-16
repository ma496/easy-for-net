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
}
