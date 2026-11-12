namespace Backend.Features.Tenancy.Core.FeatureManagement;

/// <summary>
/// Names of the value providers consulted when a feature value is resolved, in the order they are
/// asked.
/// </summary>
/// <remarks>
/// The order is the whole of the fallback rule: a tenant's own override beats what its edition grants,
/// which beats what the deployment configured, which beats the value the definition declares. The first
/// provider to answer wins, and <see cref="Default"/> always answers, so resolution never fails.
/// </remarks>
[AllowOutside]
public static class FeatureValueProviderNames
{
    /// <summary>One tenant's own override, keyed by tenant id.</summary>
    public const string Tenant = "Tenant";

    /// <summary>What the tenant's edition grants, keyed by edition id.</summary>
    public const string Edition = "Edition";

    /// <summary>What the deployment set under the <c>FeatureManagement</c> configuration section.</summary>
    public const string Configuration = "Configuration";

    /// <summary>The value the definition itself declares. Always consulted last, and always answers.</summary>
    public const string Default = "Default";

    /// <summary>
    /// The providers whose values are stored, and which may therefore be addressed by the management
    /// endpoints. Configuration and default are read-only fallbacks and belong to nobody.
    /// </summary>
    public static readonly IReadOnlySet<string> Storable =
        new HashSet<string>(StringComparer.Ordinal) { Tenant, Edition };
}
