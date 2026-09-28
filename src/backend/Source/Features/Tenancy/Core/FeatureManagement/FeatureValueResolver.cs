namespace Backend.Features.Tenancy.Core.FeatureManagement;

/// <summary>
/// Resolves feature values for an explicitly named target.
/// </summary>
/// <remarks>
/// The target is a parameter and never ambient state, which is what lets resolution run while a
/// session's claims are being minted - at sign-in and at refresh there is no tenant scope at all, and
/// anything that reached for one would throw. Endpoint code that does have a scope should use
/// <see cref="IFeatureChecker"/> instead, which is this service with the target filled in.
/// </remarks>
[AllowOutside]
public interface IFeatureValueResolver
{
    /// <summary>
    /// Resolves every feature's value for one target, in a single pass through the provider chain.
    /// </summary>
    /// <param name="target">Who the values are being resolved for.</param>
    /// <param name="ct">Token used to cancel the reads.</param>
    /// <returns>The effective values, with the provider each came from.</returns>
    Task<FeatureValueSet> ResolveAsync(FeatureTarget target, CancellationToken ct = default);

    /// <summary>
    /// Resolves every feature's value as an edition grants it - what a tenant put on that plan, and
    /// overriding nothing, would get.
    /// </summary>
    /// <remarks>
    /// An edition belongs to no tenant, so it is not a <see cref="FeatureTarget"/> and the tenant link
    /// in the chain does not apply: the chain runs from the edition down through configuration to the
    /// declared defaults. This is what the management screen edits a plan against.
    /// </remarks>
    /// <param name="editionId">The plan being resolved.</param>
    /// <param name="ct">Token used to cancel the reads.</param>
    /// <returns>The effective values, with the provider each came from.</returns>
    Task<FeatureValueSet> ResolveForEditionAsync(Guid editionId, CancellationToken ct = default);
}

/// <summary>
/// Default <see cref="IFeatureValueResolver"/> implementation, walking the provider chain in
/// registration order and keeping the first value each feature is given.
/// </summary>
/// <remarks>
/// Nothing is cached, deliberately. Resolution is one indexed read per call and happens on the few
/// paths that need it - minting a session, serving the permission catalogue, an explicit check - so a
/// cache would buy little and cost an invalidation protocol. It would also make a value written
/// earlier in the same request invisible to a read later in it, which is exactly the kind of bug that
/// is hard to see and easy to ship.
/// </remarks>
[NoDirectUse]
public class FeatureValueResolver(IEnumerable<IFeatureValueProvider> providers,
                                  IFeatureValueStore featureValueStore,
                                  IFeatureDefinitionService featureDefinitionService)
    : IFeatureValueResolver
{
    /// <inheritdoc/>
    public async Task<FeatureValueSet> ResolveAsync(FeatureTarget target, CancellationToken ct = default)
    {
        var values = new Dictionary<string, ResolvedFeatureValue>(StringComparer.Ordinal);
        foreach (var provider in providers)
        {
            Merge(values, provider.Name, await provider.GetAllAsync(target, ct));
        }
        return new FeatureValueSet(values, featureDefinitionService);
    }

    /// <inheritdoc/>
    public async Task<FeatureValueSet> ResolveForEditionAsync(Guid editionId, CancellationToken ct = default)
    {
        var values = new Dictionary<string, ResolvedFeatureValue>(StringComparer.Ordinal);

        Merge(values,
              FeatureValueProviderNames.Edition,
              await featureValueStore.GetAllAsync(FeatureValueProviderNames.Edition, editionId.ToString(), ct));

        // The remaining links are the ones that describe the installation rather than any customer, so
        // they answer the same whoever is asking and are taken from the same providers the tenant chain
        // uses - there is no second copy of "what the default is" to fall out of step.
        foreach (var provider in providers.Where(candidate =>
                     candidate.Name is FeatureValueProviderNames.Configuration or FeatureValueProviderNames.Default))
        {
            Merge(values, provider.Name, await provider.GetAllAsync(FeatureTarget.Platform, ct));
        }

        return new FeatureValueSet(values, featureDefinitionService);
    }

    /// <summary>
    /// Adds a provider's values to the result, keeping whatever an earlier provider already supplied.
    /// </summary>
    /// <remarks>
    /// The first provider to answer wins, so a tenant's own override is never displaced by what its
    /// edition grants. A value whose feature nothing declares is a leftover from a removed feature -
    /// the seeder prunes those, and until it does they are ignored rather than reported as a feature.
    /// </remarks>
    private void Merge(Dictionary<string, ResolvedFeatureValue> values,
                       string providerName,
                       IReadOnlyDictionary<string, string> supplied)
    {
        foreach (var (name, value) in supplied)
        {
            if (values.ContainsKey(name))
            {
                continue;
            }

            var feature = featureDefinitionService.GetOrNull(name);
            if (feature is null || !feature.AllowsProvider(providerName))
            {
                continue;
            }

            values[name] = new ResolvedFeatureValue(value, providerName);
        }
    }
}
