namespace Backend.Features.Tenancy.Core;

using Backend.Features.Tenancy.Core.Entities;

/// <summary>
/// Defines lookup and lifecycle operations for <see cref="Edition"/> - the plans the platform sells.
/// </summary>
/// <remarks>
/// An edition belongs to no tenant, so every operation here is a platform one and none of them reads
/// the active tenant scope.
/// </remarks>
[AllowOutside]
public interface IEditionService
{
    /// <summary>
    /// The refusal reported when a plan name is already taken.
    /// </summary>
    const string DuplicateNameMessage = "An edition with this name already exists";

    /// <summary>
    /// The refusal reported when a plan cannot be deleted because tenants are still on it.
    /// </summary>
    const string InUseMessage = "This edition cannot be deleted while tenants are on it";

    /// <summary>
    /// The editions that have not been deleted, as a query the caller narrows further.
    /// </summary>
    /// <returns>A query over the live editions.</returns>
    IQueryable<Edition> Editions();

    /// <summary>
    /// Looks an edition up by identity.
    /// </summary>
    /// <param name="id">The edition wanted.</param>
    /// <param name="cancellationToken">Token used to cancel the read.</param>
    /// <returns>The edition, or <see langword="null"/> when there is none.</returns>
    Task<Edition?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether a plan name is already taken.
    /// </summary>
    /// <remarks>
    /// Compared against the stored normalized form and deliberately counting deleted editions, so a
    /// name differing only in case, or freed only by deletion, is still taken - the same rule a tenant
    /// identifier follows, and the unique index is the backstop when two requests ask at once.
    /// </remarks>
    /// <param name="name">The name being claimed.</param>
    /// <param name="excludingId">An edition to ignore, for an update that keeps its own name.</param>
    /// <param name="cancellationToken">Token used to cancel the read.</param>
    /// <returns><see langword="true"/> when the name is taken.</returns>
    Task<bool> NameExistsAsync(string name, Guid? excludingId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// How many live tenants are on an edition. What makes deleting one refusable.
    /// </summary>
    /// <param name="id">The edition being asked about.</param>
    /// <param name="cancellationToken">Token used to cancel the read.</param>
    /// <returns>The number of tenants on that plan.</returns>
    Task<int> TenantCountAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// How many live tenants are on each of several editions, asked once for a whole page.
    /// </summary>
    /// <param name="ids">The editions being asked about.</param>
    /// <param name="cancellationToken">Token used to cancel the read.</param>
    /// <returns>Edition id to tenant count, omitting editions nobody is on.</returns>
    Task<IReadOnlyDictionary<Guid, int>> TenantCountsAsync(IReadOnlyCollection<Guid> ids,
                                                           CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a new edition.
    /// </summary>
    /// <param name="edition">The edition to create.</param>
    /// <param name="cancellationToken">Token used to cancel the write.</param>
    /// <returns>The created edition, carrying its assigned identity.</returns>
    Task<Edition> CreateAsync(Edition edition, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists changes to an edition.
    /// </summary>
    /// <param name="edition">The edition to save.</param>
    /// <param name="cancellationToken">Token used to cancel the write.</param>
    Task UpdateAsync(Edition edition, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes an edition, along with the feature values it held.
    /// </summary>
    /// <param name="edition">The edition to delete.</param>
    /// <param name="cancellationToken">Token used to cancel the write.</param>
    Task DeleteAsync(Edition edition, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default <see cref="IEditionService"/> implementation.
/// </summary>
[NoDirectUse]
public class EditionService(AppDbContext dbContext, IFeatureValueStore featureValueStore) : IEditionService
{
    /// <inheritdoc/>
    public IQueryable<Edition> Editions() => dbContext.Editions;

    /// <inheritdoc/>
    public async Task<Edition?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await dbContext.Editions.FirstOrDefaultAsync(edition => edition.Id == id, cancellationToken);

    /// <inheritdoc/>
    public async Task<bool> NameExistsAsync(string name,
                                            Guid? excludingId = null,
                                            CancellationToken cancellationToken = default)
    {
        var normalized = name.Trim().ToLowerInvariant();
        return await dbContext.Editions
            .IgnoreQueryFilters()
            .AnyAsync(edition => edition.NameNormalized == normalized
                                 && (excludingId == null || edition.Id != excludingId), cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<int> TenantCountAsync(Guid id, CancellationToken cancellationToken = default)
        => await dbContext.Tenants
            .AsNoTracking()
            .AcrossAllTenants()
            .CountAsync(tenant => tenant.EditionId == id, cancellationToken);

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<Guid, int>> TenantCountsAsync(IReadOnlyCollection<Guid> ids,
                                                                        CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        var counts = await dbContext.Tenants
            .AsNoTracking()
            .AcrossAllTenants()
            .Where(tenant => tenant.EditionId != null && ids.Contains(tenant.EditionId.Value))
            .GroupBy(tenant => tenant.EditionId!.Value)
            .Select(group => new { EditionId = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        return counts.ToDictionary(entry => entry.EditionId, entry => entry.Count);
    }

    /// <inheritdoc/>
    public async Task<Edition> CreateAsync(Edition edition, CancellationToken cancellationToken = default)
    {
        edition.Name = edition.Name.Trim();
        dbContext.Editions.Add(edition);
        await dbContext.SaveChangesAsync(cancellationToken);
        return edition;
    }

    /// <inheritdoc/>
    public async Task UpdateAsync(Edition edition, CancellationToken cancellationToken = default)
    {
        edition.Name = edition.Name.Trim();
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(Edition edition, CancellationToken cancellationToken = default)
    {
        // The feature values go with it: they carry no foreign key to the edition, so nothing else
        // would ever remove them, and a new edition reusing the identity is not a thing that happens.
        await featureValueStore.DeleteAllAsync(FeatureValueProviderNames.Edition,
                                               edition.Id.ToString(),
                                               cancellationToken);

        dbContext.Editions.Remove(edition);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
