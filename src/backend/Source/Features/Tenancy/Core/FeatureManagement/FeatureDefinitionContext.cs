namespace Backend.Features.Tenancy.Core.FeatureManagement;

/// <summary>
/// Mutable builder context passed to <see cref="IFeatureDefinitionProvider"/> implementations while
/// they declare the features they contribute.
/// </summary>
/// <remarks>
/// A context is created, handed to a single provider and then discarded by
/// <see cref="FeatureDefinitionService"/>, so the catalogue it produces is fixed once the provider
/// returns and cannot be extended at run time. This mirrors
/// <see cref="Backend.Permissions.PermissionDefinitionContext"/> deliberately: what a tenant may be
/// entitled to is as much a property of the code as what a role may be granted.
/// </remarks>
[AllowOutside]
public class FeatureDefinitionContext
{
    private IList<FeatureDefinition> _features { get; } = [];

    /// <summary>
    /// Adds a new top-level feature to the context.
    /// </summary>
    /// <param name="name">Stable feature name, for example <c>Identity.UserProvisioning</c>.</param>
    /// <param name="displayName">Human-readable name shown in the management UI.</param>
    /// <param name="defaultValue">
    /// The value used when no provider supplies one. Ship features enabled by default, so a generated
    /// project behaves as though the entitlement system were not there until it chooses to use it.
    /// </param>
    /// <param name="valueType">What kind of value the feature holds. A toggle unless stated.</param>
    /// <param name="description">Longer explanation shown beneath the field in the management UI.</param>
    /// <param name="isVisibleToClients">Whether the value may be reported to the browser.</param>
    /// <returns>The created <see cref="FeatureDefinition"/>, which can be used to add child features.</returns>
    public FeatureDefinition AddFeature(string name,
                                        string displayName,
                                        string? defaultValue = null,
                                        FeatureValueType? valueType = null,
                                        string? description = null,
                                        bool isVisibleToClients = true)
    {
        var feature = new FeatureDefinition(name, displayName, defaultValue, valueType, description, isVisibleToClients);
        _features.Add(feature);
        return feature;
    }

    /// <summary>
    /// Returns a read-only view of the features that have been added to this context.
    /// </summary>
    /// <returns>The features defined so far in this context.</returns>
    public IReadOnlyList<FeatureDefinition> GetFeatures()
    {
        return _features.AsReadOnly();
    }
}
