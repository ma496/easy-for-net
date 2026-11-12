namespace Backend.Features.Tenancy.Core.FeatureManagement;

/// <summary>
/// One feature's effective value, and which provider in the chain supplied it.
/// </summary>
/// <param name="Value">The value, or <see langword="null"/> when no provider supplied one.</param>
/// <param name="ProviderName">
/// The provider the value came from, which is what lets the management UI say a value is inherited
/// from an edition rather than set on the tenant.
/// </param>
[AllowOutside]
public sealed record ResolvedFeatureValue(string? Value, string ProviderName);

/// <summary>
/// Every feature's effective value for one target, resolved in a single pass through the chain.
/// </summary>
/// <remarks>
/// Resolution is done in bulk and handed out as a set rather than asked for one feature at a time,
/// because the callers that matter - minting a session, serving the permission catalogue - need the
/// whole answer at once and would otherwise turn one indexed read into dozens.
/// </remarks>
[AllowOutside]
public sealed class FeatureValueSet
{
    private readonly IReadOnlyDictionary<string, ResolvedFeatureValue> _values;
    private readonly IFeatureDefinitionService _featureDefinitionService;

    public FeatureValueSet(IReadOnlyDictionary<string, ResolvedFeatureValue> values,
                           IFeatureDefinitionService featureDefinitionService)
    {
        _values = values;
        _featureDefinitionService = featureDefinitionService;
    }

    /// <summary>
    /// The resolved values, by feature name.
    /// </summary>
    public IReadOnlyDictionary<string, ResolvedFeatureValue> Values => _values;

    /// <summary>
    /// The effective value of one feature.
    /// </summary>
    /// <param name="name">The feature name.</param>
    /// <returns>The value, or <see langword="null"/> when no provider supplied one.</returns>
    public string? GetOrNull(string name) => _values.GetValueOrDefault(name)?.Value;

    /// <summary>
    /// Which provider supplied a feature's value.
    /// </summary>
    /// <param name="name">The feature name.</param>
    /// <returns>The provider name, or <see langword="null"/> when nothing supplied a value.</returns>
    public string? ProviderOf(string name) => _values.GetValueOrDefault(name)?.ProviderName;

    /// <summary>
    /// Whether a feature is enabled: its own value reads as true, and so does every toggle above it.
    /// </summary>
    /// <remarks>
    /// The ancestor rule is what makes a child feature worth declaring as a child. Switching a parent
    /// off switches off everything beneath it, so a child need not restate its parent's condition and
    /// cannot contradict it - which is a stronger guarantee than treating the nesting as a hint to the
    /// editor and leaving the two values free to disagree.
    /// </remarks>
    /// <param name="name">The feature name.</param>
    /// <returns><see langword="true"/> when the feature and every ancestor toggle are enabled.</returns>
    public bool IsEnabled(string name)
    {
        var feature = _featureDefinitionService.GetOrNull(name);
        if (feature is null)
        {
            return false;
        }

        for (var node = feature; node is not null; node = node.Parent)
        {
            if (node.ValueType is not ToggleValueType)
            {
                continue;
            }
            if (!ReadsAsTrue(GetOrNull(node.Name)))
            {
                return false;
            }
        }
        return true;
    }

    private static bool ReadsAsTrue(string? value)
        => value is not null && string.Equals(value, BooleanValidator.TrueValue, StringComparison.OrdinalIgnoreCase);
}
