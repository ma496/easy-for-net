namespace Backend.Features.Tenancy.Core.FeatureManagement;

using System.Text.Json.Serialization;

/// <summary>
/// Describes a single feature - one thing a tenant's plan either grants or withholds - and supports a
/// tree of child features used to model dependent capabilities.
/// </summary>
/// <remarks>
/// The catalogue is declared in code by the <see cref="IFeatureDefinitionProvider"/>s and is the same
/// for every tenant; only the <em>values</em> vary. Unlike a permission, where a node with children is
/// a display grouping and never grantable, every feature node is itself a real feature with its own
/// value - a parent toggle switches off a whole area, and its children refine what is left.
/// </remarks>
/// <param name="name">Stable feature name, for example <c>Identity.UserProvisioning</c>.</param>
/// <param name="displayName">Human-readable name shown in the management UI.</param>
/// <param name="defaultValue">
/// The value used when no provider supplies one. Every feature this template ships defaults to enabled,
/// so a fresh installation behaves as though the system were not there.
/// </param>
/// <param name="valueType">What kind of value the feature holds. A toggle unless stated.</param>
/// <param name="description">Longer explanation shown beneath the field in the management UI.</param>
/// <param name="isVisibleToClients">
/// Whether the value may be reported to the browser. Leave it off for a feature that describes
/// something the client has no business knowing.
/// </param>
[AllowOutside]
public class FeatureDefinition(string name,
                               string displayName,
                               string? defaultValue = null,
                               FeatureValueType? valueType = null,
                               string? description = null,
                               bool isVisibleToClients = true)
{
    public string Name { get; } = name;
    public string DisplayName { get; set; } = displayName;
    public string? Description { get; set; } = description;
    public string? DefaultValue { get; set; } = defaultValue;

    /// <summary>
    /// What kind of value this feature holds, which decides both validation and the editor control.
    /// </summary>
    public FeatureValueType ValueType { get; set; } = valueType ?? new ToggleValueType();

    /// <summary>
    /// Whether the resolved value may be reported to the browser by the account's own features endpoint.
    /// </summary>
    public bool IsVisibleToClients { get; set; } = isVisibleToClients;

    /// <summary>
    /// The value providers permitted to set this feature, by name. Empty means every provider may,
    /// which is the usual case; naming providers is how a feature is confined to, say, editions alone.
    /// The default provider always applies, because it is the definition's own fallback.
    /// </summary>
    public IReadOnlyList<string> AllowedProviders { get; private set; } = [];

    /// <summary>
    /// The feature this one hangs beneath, or <see langword="null"/> for a root feature.
    /// </summary>
    /// <remarks>
    /// Ignored when serialized: the parent link is what makes the tree navigable in memory, and writing
    /// it out would hand the management UI a cycle.
    /// </remarks>
    [JsonIgnore]
    public FeatureDefinition? Parent { get; internal set; }

    public IList<FeatureDefinition> Children { get; } = [];

    /// <summary>
    /// Creates and attaches a child feature beneath this one. A child resolves disabled whenever an
    /// ancestor toggle resolves disabled, so a child need not restate its parent's condition.
    /// </summary>
    /// <param name="name">Stable name of the child feature.</param>
    /// <param name="displayName">Display name of the child feature.</param>
    /// <param name="defaultValue">The child's fallback value.</param>
    /// <param name="valueType">What kind of value the child holds. A toggle unless stated.</param>
    /// <param name="description">Longer explanation of the child feature.</param>
    /// <param name="isVisibleToClients">Whether the child's value may be reported to the browser.</param>
    /// <returns>The newly created child <see cref="FeatureDefinition"/>.</returns>
    public FeatureDefinition AddChild(string name,
                                      string displayName,
                                      string? defaultValue = null,
                                      FeatureValueType? valueType = null,
                                      string? description = null,
                                      bool isVisibleToClients = true)
    {
        var child = new FeatureDefinition(name, displayName, defaultValue, valueType, description, isVisibleToClients)
        {
            Parent = this
        };
        Children.Add(child);
        return child;
    }

    /// <summary>
    /// Confines this feature to the named value providers, so a provider outside the list may neither
    /// store a value for it nor be consulted when one is resolved.
    /// </summary>
    /// <param name="providerNames">Names from <see cref="FeatureValueProviderNames"/>.</param>
    /// <returns>This definition, so a declaration reads as one statement.</returns>
    public FeatureDefinition AllowProviders(params string[] providerNames)
    {
        AllowedProviders = providerNames;
        return this;
    }

    /// <summary>
    /// Whether a value provider may supply a value for this feature. The default provider always may,
    /// since it is the definition's own fallback rather than something anyone stored.
    /// </summary>
    /// <param name="providerName">The provider being consulted.</param>
    /// <returns><see langword="true"/> when the provider is permitted.</returns>
    public bool AllowsProvider(string providerName)
        => providerName == FeatureValueProviderNames.Default
           || AllowedProviders.Count == 0
           || AllowedProviders.Contains(providerName, StringComparer.Ordinal);
}
