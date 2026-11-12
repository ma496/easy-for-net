namespace Backend.Features.Tenancy.Core.FeatureManagement.Providers;

/// <summary>
/// Supplies the values a tenant has set for itself. First in the chain, so a tenant's own override
/// beats whatever its edition grants.
/// </summary>
[NoDirectUse]
public class TenantFeatureValueProvider(IFeatureValueStore store) : IFeatureValueProvider
{
    public string Name => FeatureValueProviderNames.Tenant;

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, string>> GetAllAsync(FeatureTarget target,
                                                                       CancellationToken ct = default)
        => target.TenantId is { } tenantId
            ? await store.GetAllAsync(Name, tenantId.ToString(), ct)
            : NoFeatureValues.Values;
}
