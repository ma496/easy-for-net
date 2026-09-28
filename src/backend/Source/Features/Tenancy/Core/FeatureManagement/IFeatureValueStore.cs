namespace Backend.Features.Tenancy.Core.FeatureManagement;

/// <summary>
/// Reads and writes the stored feature values, addressed by the provider that set them.
/// </summary>
/// <remarks>
/// Every method states its <c>providerName</c> and <c>providerKey</c>, which is what lets the store be
/// used while a session is minted: the rows carry no tenant column and no tenant query filter, so the
/// key in the call is the whole of the attribution and nothing is read from ambient scope.
/// </remarks>
[AllowOutside]
public interface IFeatureValueStore
{
    /// <summary>
    /// Returns every value one provider holds for one key, by feature name, in a single read.
    /// </summary>
    /// <param name="providerName">The provider, from <see cref="FeatureValueProviderNames"/>.</param>
    /// <param name="providerKey">The tenant or edition the values belong to.</param>
    /// <param name="ct">Token used to cancel the read.</param>
    /// <returns>The stored values, keyed by feature name.</returns>
    Task<IReadOnlyDictionary<string, string>> GetAllAsync(string providerName, string providerKey, CancellationToken ct = default);

    /// <summary>
    /// Stores a value, replacing any the provider already holds for that feature. A
    /// <see langword="null"/> value deletes the row instead, which is what makes the feature fall
    /// through to the next provider.
    /// </summary>
    /// <param name="name">The feature the value is for.</param>
    /// <param name="value">The value to store, or <see langword="null"/> to clear the override.</param>
    /// <param name="providerName">The provider setting it.</param>
    /// <param name="providerKey">The tenant or edition it is set for.</param>
    /// <param name="ct">Token used to cancel the write.</param>
    Task SetAsync(string name, string? value, string providerName, string providerKey, CancellationToken ct = default);

    /// <summary>
    /// Deletes every value one provider holds for one key. Used when the thing the key names is
    /// deleted, since the rows carry no foreign key to follow.
    /// </summary>
    /// <param name="providerName">The provider whose values are being removed.</param>
    /// <param name="providerKey">The tenant or edition being cleared.</param>
    /// <param name="ct">Token used to cancel the write.</param>
    Task DeleteAllAsync(string providerName, string providerKey, CancellationToken ct = default);

    /// <summary>
    /// Deletes stored values whose feature no longer appears in the code-declared catalogue, so a
    /// renamed or removed feature leaves no value nobody can see or clear.
    /// </summary>
    /// <param name="knownNames">Every feature name the catalogue declares.</param>
    /// <param name="ct">Token used to cancel the write.</param>
    /// <returns>How many rows were removed.</returns>
    Task<int> PruneUnknownAsync(IReadOnlySet<string> knownNames, CancellationToken ct = default);
}
