namespace Backend.Features.Tenancy.Core.FeatureManagement.Providers;

/// <summary>
/// Supplies the values the tenant's edition grants - what the plan it is on includes.
/// </summary>
/// <remarks>
/// A tenant on no edition, which is what a fresh installation looks like, simply contributes nothing
/// here and falls through to configuration and the declared defaults.
/// </remarks>
[NoDirectUse]
public class EditionFeatureValueProvider(IFeatureValueStore store, AppDbContext dbContext) : IFeatureValueProvider
{
    public string Name => FeatureValueProviderNames.Edition;

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, string>> GetAllAsync(FeatureTarget target,
                                                                       CancellationToken ct = default)
    {
        if (target.TenantId is not { } tenantId)
        {
            return NoFeatureValues.Values;
        }

        var editionId = await EditionOfAsync(tenantId, ct);
        return editionId is { } edition
            ? await store.GetAllAsync(Name, edition.ToString(), ct)
            : NoFeatureValues.Values;
    }

    /// <summary>
    /// The plan a tenant is on, or <see langword="null"/> when it is on none.
    /// </summary>
    /// <remarks>
    /// Deliberately reached through the <c>Editions</c> set rather than by reading
    /// <c>Tenant.EditionId</c> alone: that column would still name a plan that has been deleted, and
    /// going through the set is what puts the soft-delete filter in the way, so a deleted edition
    /// stops granting what it granted. Nobody should meet that in normal use - deleting an edition a
    /// tenant is on is refused - but if it happens, the tenant falls back to the declared defaults
    /// rather than keeping a plan that no longer exists.
    /// <para>
    /// A tenant row carries no tenant of its own - it is the scope - so this reads without
    /// establishing one, which is what lets it run while a session is being minted.
    /// </para>
    /// </remarks>
    /// <param name="tenantId">The tenant being asked about.</param>
    /// <param name="ct">Token used to cancel the read.</param>
    /// <returns>The edition id, or <see langword="null"/>.</returns>
    private async Task<Guid?> EditionOfAsync(Guid tenantId, CancellationToken ct)
        => await dbContext.Tenants
            .AsNoTracking()
            .Where(tenant => tenant.Id == tenantId)
            .SelectMany(tenant => dbContext.Editions.Where(edition => edition.Id == tenant.EditionId))
            .Select(edition => (Guid?)edition.Id)
            .FirstOrDefaultAsync(ct);
}
