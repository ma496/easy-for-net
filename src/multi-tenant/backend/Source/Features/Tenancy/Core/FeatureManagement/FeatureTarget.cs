namespace Backend.Features.Tenancy.Core.FeatureManagement;

/// <summary>
/// Who a feature value is being resolved for.
/// </summary>
/// <remarks>
/// This exists so resolution never has to consult ambient state. Feature values are read while a
/// session's claims are minted - at sign-in and at refresh - where no tenant scope has been established
/// and <c>AppDbContext.CurrentTenantId</c> would throw. Naming the target in the call is what makes
/// that safe, and it is the same move <c>SessionGrants</c> already makes by taking the tenant as a
/// parameter rather than reading it from the request.
/// </remarks>
/// <param name="TenantId">The tenant being resolved for, or <see langword="null"/> for the platform.</param>
[AllowOutside]
public readonly record struct FeatureTarget(Guid? TenantId)
{
    /// <summary>
    /// The platform itself, acting in no tenant and therefore inside nobody's plan.
    /// </summary>
    public static readonly FeatureTarget Platform = new((Guid?)null);

    /// <summary>
    /// Resolution for one tenant.
    /// </summary>
    /// <param name="tenantId">The tenant whose values are wanted.</param>
    /// <returns>A target naming that tenant.</returns>
    public static FeatureTarget ForTenant(Guid tenantId) => new(tenantId);

    /// <summary>
    /// Whether this target is the platform rather than a tenant.
    /// </summary>
    public bool IsPlatform => TenantId is null;
}
