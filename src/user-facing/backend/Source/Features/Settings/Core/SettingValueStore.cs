using Backend.Features.Settings.Core.Entities;

namespace Backend.Features.Settings.Core;

/// <summary>
/// Reads and writes the stored overrides of settings. Internal to the <c>Settings</c> slice: other
/// slices read resolved values through <see cref="ISettingProvider"/> and never see a row.
/// </summary>
interface ISettingValueStore
{
    /// <summary>Reads the stored overrides of one setting as raw JSON text.</summary>
    /// <param name="name">The setting's registered name.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The overrides, or <see langword="null"/> when the setting has no row.</returns>
    Task<string?> GetValuesAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the stored overrides of one setting with <paramref name="values"/>, creating the row if
    /// there is none.
    /// </summary>
    /// <param name="name">The setting's registered name.</param>
    /// <param name="values">A JSON object of only the properties overridden.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    Task SetOwnAsync(string name, string values, CancellationToken cancellationToken = default);

    /// <summary>Removes the row for one setting, if it has one. Idempotent.</summary>
    /// <param name="name">The setting's registered name.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    Task DeleteOwnAsync(string name, CancellationToken cancellationToken = default);
}

/// <summary>EF Core-backed implementation of <see cref="ISettingValueStore"/>.</summary>
[NoDirectUse]
class SettingValueStore(AppDbContext dbContext) : ISettingValueStore
{
    /// <inheritdoc />
    public async Task<string?> GetValuesAsync(string name, CancellationToken cancellationToken = default)
        => await dbContext.SettingValues
            .AsNoTracking()
            .Where(x => x.Name == name)
            .Select(x => x.Values)
            .FirstOrDefaultAsync(cancellationToken);

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
