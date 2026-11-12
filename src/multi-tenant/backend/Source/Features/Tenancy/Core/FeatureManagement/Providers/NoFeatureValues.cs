namespace Backend.Features.Tenancy.Core.FeatureManagement.Providers;

/// <summary>
/// The empty answer a value provider gives when it holds nothing for a target - a tenant provider
/// asked about the platform, or an edition provider asked about a tenant on no plan.
/// </summary>
static class NoFeatureValues
{
    public static readonly IReadOnlyDictionary<string, string> Values =
        new Dictionary<string, string>(StringComparer.Ordinal);
}
