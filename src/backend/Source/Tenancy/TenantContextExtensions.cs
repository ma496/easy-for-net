namespace Backend.Tenancy;

/// <summary>
/// Convenience readings of the active tenant scope, so that the question "is this request acting on
/// the platform rather than inside a tenant?" is asked the same way everywhere instead of being
/// spelled out again at each call site.
/// </summary>
public static class TenantContextExtensions
{
    /// <summary>
    /// Whether the request is acting in platform scope - a scope has been established and it names no
    /// tenant. It is deliberately false while no scope has been established at all, so a caller that
    /// reaches this before the tenant pre-processor has run is treated as not being on the platform
    /// rather than as being on it, and it never throws the way reading the tenant directly would.
    /// </summary>
    /// <param name="tenantContext">The ambient tenant scope of the current unit of work.</param>
    /// <returns><see langword="true"/> when the active scope is platform scope.</returns>
    public static bool IsPlatformScope(this ITenantContext tenantContext)
        => tenantContext is { IsResolved: true, CurrentTenantId: null };
}
