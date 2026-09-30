namespace Backend.Features.Settings.Core;

using Backend.Features.Settings.Core.Entities;

/// <summary>
/// Reads and writes the stored overrides of settings. Internal to the <c>Settings</c> slice: other
/// slices read resolved values through <see cref="ISettingProvider"/> and never see a row.
/// </summary>
interface ISettingValueStore
{
    /// <summary>
    /// Reads the overrides that bear on one setting for one target: the platform's row, and - when
    /// <paramref name="tenantId"/> names a tenant - that tenant's own.
    /// </summary>
    /// <param name="name">The setting's registered name.</param>
    /// <param name="tenantId">The tenant resolved for, or <see langword="null"/> for the platform.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    Task<SettingRows> GetRowsAsync(string name, Guid? tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the acting scope's overrides of one setting with <paramref name="values"/> - the
    /// tenant's row inside a tenant, the platform's in platform scope - creating the row if there is none.
    /// </summary>
    /// <param name="name">The setting's registered name.</param>
    /// <param name="values">A JSON object of only the properties the scope overrides.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    Task SetOwnAsync(string name, string values, CancellationToken cancellationToken = default);

    /// <summary>Removes the acting scope's row for one setting, if it has one. Idempotent.</summary>
    /// <param name="name">The setting's registered name.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    Task DeleteOwnAsync(string name, CancellationToken cancellationToken = default);
}

/// <summary>The stored overrides that bear on one setting for one target, as their raw JSON text.</summary>
/// <param name="PlatformValues">The platform row's overrides, or <see langword="null"/> when there is no platform row.</param>
/// <param name="TenantValues">The tenant row's overrides, or <see langword="null"/> when there is none or no tenant was asked about.</param>
sealed record SettingRows(string? PlatformValues, string? TenantValues);

/// <summary>EF Core-backed implementation of <see cref="ISettingValueStore"/>.</summary>
/// <remarks>
/// Reads name their tenant explicitly and lift only the tenant filter to do it, the way the
/// localization overrides are read: resolution runs for a tenant other than the acting one (sign-in
/// resolves the tenant being entered, before any scope exists), and the platform row has to be read
/// from inside a tenant. Writes go through the tenant-filtered set, so they can only ever find and
/// change the acting scope's own row, and <see cref="AppDbContext"/> stamps a new row with that scope.
/// </remarks>
[NoDirectUse]
class SettingValueStore(AppDbContext dbContext) : ISettingValueStore
{
    /// <inheritdoc />
    public async Task<SettingRows> GetRowsAsync(string name, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        var rows = await dbContext.SettingValues
            .AsNoTracking()
            .AcrossAllTenants()
            .Where(x => x.Name == name && (x.TenantId == tenantId || x.TenantId == null))
            .Select(x => new { x.TenantId, x.Values })
            .ToListAsync(cancellationToken);

        var platform = rows.FirstOrDefault(x => x.TenantId == null)?.Values;
        var tenant = tenantId is null ? null : rows.FirstOrDefault(x => x.TenantId == tenantId)?.Values;
        return new SettingRows(platform, tenant);
    }

    /// <inheritdoc />
    public async Task SetOwnAsync(string name, string values, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.SettingValues.FirstOrDefaultAsync(x => x.Name == name, cancellationToken);
        if (entity is null)
        {
            entity = new SettingValue { Name = name };
            dbContext.SettingValues.Add(entity);
        }

        entity.Values = values;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteOwnAsync(string name, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.SettingValues.FirstOrDefaultAsync(x => x.Name == name, cancellationToken);
        if (entity is null)
        {
            return;
        }

        dbContext.SettingValues.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}