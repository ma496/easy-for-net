namespace Backend.Features.Tenancy.Core.FeatureManagement;

/// <summary>
/// Bundles a set of related <see cref="FeatureDefinition"/>s under a common <see cref="GroupName"/>,
/// which the management UI renders as one entry in its group list.
/// </summary>
[AllowOutside]
public class FeatureGroupDefinition
{
    public string GroupName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public IReadOnlyList<FeatureDefinition> Features { get; set; } = [];
}
