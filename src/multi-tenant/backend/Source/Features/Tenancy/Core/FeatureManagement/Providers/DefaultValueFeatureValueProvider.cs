namespace Backend.Features.Tenancy.Core.FeatureManagement.Providers;

/// <summary>
/// Supplies the value each definition declares for itself. Last in the chain, and the reason
/// resolution never fails: every feature this template ships states a default.
/// </summary>
[NoDirectUse]
public class DefaultValueFeatureValueProvider(IFeatureDefinitionService featureDefinitionService)
    : IFeatureValueProvider
{
    public string Name => FeatureValueProviderNames.Default;

    /// <inheritdoc/>
    public Task<IReadOnlyDictionary<string, string>> GetAllAsync(FeatureTarget target,
                                                                 CancellationToken ct = default)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var feature in featureDefinitionService.GetAll())
        {
            if (feature.DefaultValue is { } value)
            {
                values[feature.Name] = value;
            }
        }
        return Task.FromResult<IReadOnlyDictionary<string, string>>(values);
    }
}
