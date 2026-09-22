namespace Backend.Features.Tenancy.Core.FeatureManagement.Providers;

using Microsoft.Extensions.Configuration;

/// <summary>
/// Supplies the values the deployment set under the <c>FeatureManagement</c> configuration section.
/// </summary>
/// <remarks>
/// The same for every target, because it describes the installation rather than any one customer -
/// this is how a whole module is switched off for a deployment that does not want it, without touching
/// a single tenant or edition. Read-only: nothing stores a value here.
/// </remarks>
[NoDirectUse]
public class ConfigurationFeatureValueProvider(IConfiguration configuration) : IFeatureValueProvider
{
    /// <summary>
    /// Configuration section the deployment's feature values are read from.
    /// </summary>
    public const string SectionName = "FeatureManagement";

    public string Name => FeatureValueProviderNames.Configuration;

    /// <inheritdoc/>
    public Task<IReadOnlyDictionary<string, string>> GetAllAsync(FeatureTarget target,
                                                                 CancellationToken ct = default)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in configuration.GetSection(SectionName).GetChildren())
        {
            if (entry.Value is { } value)
            {
                values[entry.Key] = value;
            }
        }
        return Task.FromResult<IReadOnlyDictionary<string, string>>(values);
    }
}
