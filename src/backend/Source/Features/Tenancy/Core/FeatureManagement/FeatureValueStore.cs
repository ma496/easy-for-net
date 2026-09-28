namespace Backend.Features.Tenancy.Core.FeatureManagement;

using Backend.Features.Tenancy.Core.Entities;

/// <summary>
/// Default <see cref="IFeatureValueStore"/> implementation, and the only type that touches the
/// <see cref="FeatureValue"/> set.
/// </summary>
/// <remarks>
/// Concentrating every read and write here is what makes the entity's exemption from tenant
/// restriction safe: there is exactly one place that could forget to state a provider key, and it
/// never does.
/// </remarks>
[NoDirectUse]
public class FeatureValueStore(AppDbContext dbContext) : IFeatureValueStore
{
    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, string>> GetAllAsync(string providerName,
                                                                       string providerKey,
                                                                       CancellationToken ct = default)
    {
        var values = await dbContext.FeatureValues
            .AsNoTracking()
            .Where(value => value.ProviderName == providerName && value.ProviderKey == providerKey)
            .Select(value => new { value.Name, value.Value })
            .ToListAsync(ct);

        return values.ToDictionary(value => value.Name, value => value.Value, StringComparer.Ordinal);
    }

    /// <inheritdoc/>
    public async Task SetAsync(string name,
                               string? value,
                               string providerName,
                               string providerKey,
                               CancellationToken ct = default)
    {
        var existing = await dbContext.FeatureValues
            .FirstOrDefaultAsync(row => row.Name == name
                                        && row.ProviderName == providerName
                                        && row.ProviderKey == providerKey, ct);

        if (value is null)
        {
            if (existing is not null)
            {
                dbContext.FeatureValues.Remove(existing);
                await dbContext.SaveChangesAsync(ct);
            }
            return;
        }

        if (existing is null)
        {
            dbContext.FeatureValues.Add(new FeatureValue
            {
                Name = name,
                Value = value,
                ProviderName = providerName,
                ProviderKey = providerKey
            });
        }
        else
        {
            existing.Value = value;
        }

        await dbContext.SaveChangesAsync(ct);
    }

    /// <inheritdoc/>
    public async Task DeleteAllAsync(string providerName, string providerKey, CancellationToken ct = default)
        => await dbContext.FeatureValues
            .Where(value => value.ProviderName == providerName && value.ProviderKey == providerKey)
            .ExecuteDeleteAsync(ct);

    /// <inheritdoc/>
    public async Task<int> PruneUnknownAsync(IReadOnlySet<string> knownNames, CancellationToken ct = default)
    {
        var names = knownNames.ToList();
        return await dbContext.FeatureValues
            .Where(value => !names.Contains(value.Name))
            .ExecuteDeleteAsync(ct);
    }
}
