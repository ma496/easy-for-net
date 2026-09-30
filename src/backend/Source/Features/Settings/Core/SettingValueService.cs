namespace Backend.Features.Settings.Core;

using System.Text.Json.Nodes;

/// <summary>
/// Resolves settings layer by layer and writes the acting scope's overrides. Internal to the
/// <c>Settings</c> slice: <see cref="ISettingProvider"/> is the published, typed face of the same
/// resolution, and the slice's endpoints use this for the per-property detail and for writes.
/// </summary>
/// <remarks>
/// A value resolves property by property, first layer with an answer winning: the tenant's own
/// override, then the platform's (<c>TenantId</c> <see langword="null"/>), then the code default -
/// the setting class's property initializers. Resolving for no tenant is the platform's answer:
/// default with the platform's overrides laid over it. A stored property the class no longer has, or
/// one whose stored value no longer deserializes to the property's type, contributes nothing and the
/// property follows the layer below.
/// <para>
/// Stored rows are read once per (tenant, setting) for the lifetime of this instance - one request or
/// job, since the service is registered scoped - and a write through this instance drops what it had
/// read for that setting, so a read after a write in the same scope sees the write.
/// </para>
/// </remarks>
interface ISettingValueService
{
    /// <summary>Resolves one setting for <paramref name="tenantId"/>, or for the platform when it is <see langword="null"/>.</summary>
    Task<ResolvedSetting> ResolveAsync(SettingDefinition definition, Guid? tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves one setting for <paramref name="tenantId"/> as it would stand if that scope's own
    /// overrides were replaced by <paramref name="ownOverrides"/> - the tenant's layer when a tenant is
    /// named, the platform's when none is. Nothing is written.
    /// </summary>
    Task<ResolvedSetting> PreviewAsync(SettingDefinition definition, Guid? tenantId, JsonObject ownOverrides, CancellationToken cancellationToken = default);

    /// <summary>Replaces the acting scope's overrides of one setting with <paramref name="ownOverrides"/>; an empty object removes them.</summary>
    Task SetOwnAsync(SettingDefinition definition, JsonObject ownOverrides, CancellationToken cancellationToken = default);

    /// <summary>Removes the acting scope's overrides of one setting, if it has any. Idempotent.</summary>
    Task DeleteOwnAsync(SettingDefinition definition, CancellationToken cancellationToken = default);
}

/// <summary>One setting resolved for one target: the merged values and where each property's value came from.</summary>
/// <param name="Definition">The setting resolved.</param>
/// <param name="Values">The merged values, one member per property, under their JSON names.</param>
/// <param name="Sources">Per property JSON name, the layer that answered it - a <see cref="SettingValueSource"/> constant.</param>
sealed record ResolvedSetting(SettingDefinition Definition, JsonObject Values, IReadOnlyDictionary<string, string> Sources);

/// <summary>The layer a resolved property's value came from, as the API reports it.</summary>
static class SettingValueSource
{
    public const string Tenant = "tenant";
    public const string Platform = "platform";
    public const string Default = "default";
}

/// <summary>Default implementation of <see cref="ISettingValueService"/>.</summary>
[NoDirectUse]
class SettingValueService(ISettingValueStore store) : ISettingValueService
{
    /// <summary>The parsed stored layers, keyed by the tenant resolved for and the setting's name.</summary>
    private readonly Dictionary<(Guid? TenantId, string Name), (JsonObject? Platform, JsonObject? Tenant)> _layers = new();

    /// <inheritdoc />
    public async Task<ResolvedSetting> ResolveAsync(SettingDefinition definition, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        var (platform, tenant) = await GetLayersAsync(definition, tenantId, cancellationToken);
        return Merge(definition, platform, tenant);
    }

    /// <inheritdoc />
    public async Task<ResolvedSetting> PreviewAsync(SettingDefinition definition, Guid? tenantId, JsonObject ownOverrides, CancellationToken cancellationToken = default)
    {
        if (tenantId is null)
        {
            return Merge(definition, ownOverrides, null);
        }

        var (platform, _) = await GetLayersAsync(definition, tenantId, cancellationToken);
        return Merge(definition, platform, ownOverrides);
    }

    /// <inheritdoc />
    public async Task SetOwnAsync(SettingDefinition definition, JsonObject ownOverrides, CancellationToken cancellationToken = default)
    {
        if (ownOverrides.Count == 0)
        {
            await store.DeleteOwnAsync(definition.Name, cancellationToken);
        }
        else
        {
            await store.SetOwnAsync(definition.Name, ownOverrides.ToJsonString(SettingJson.Options), cancellationToken);
        }

        Forget(definition);
    }

    /// <inheritdoc />
    public async Task DeleteOwnAsync(SettingDefinition definition, CancellationToken cancellationToken = default)
    {
        await store.DeleteOwnAsync(definition.Name, cancellationToken);
        Forget(definition);
    }

    /// <summary>
    /// Drops every cached read of one setting. A platform write changes what every tenant resolves to,
    /// so the setting is forgotten for every target rather than only the one written.
    /// </summary>
    private void Forget(SettingDefinition definition)
    {
        foreach (var key in _layers.Keys.Where(key => key.Name == definition.Name).ToList())
        {
            _layers.Remove(key);
        }
    }

    private async Task<(JsonObject? Platform, JsonObject? Tenant)> GetLayersAsync(SettingDefinition definition, Guid? tenantId, CancellationToken cancellationToken)
    {
        var key = (tenantId, definition.Name);
        if (_layers.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var rows = await store.GetRowsAsync(definition.Name, tenantId, cancellationToken);
        var layers = (SettingJson.ParseObject(rows.PlatformValues), SettingJson.ParseObject(rows.TenantValues));
        _layers[key] = layers;
        return layers;
    }

    /// <summary>
    /// Lays the platform layer and then the tenant layer over a fresh copy of the code default. The
    /// layers are never modified - values are copied out of them - so a cached layer serves every read.
    /// </summary>
    private static ResolvedSetting Merge(SettingDefinition definition, JsonObject? platform, JsonObject? tenant)
    {
        var values = definition.CreateDefaultValues();
        var sources = definition.Properties.ToDictionary(property => property.Name, _ => SettingValueSource.Default, StringComparer.Ordinal);

        Lay(platform, SettingValueSource.Platform);
        Lay(tenant, SettingValueSource.Tenant);

        return new ResolvedSetting(definition, values, sources);

        void Lay(JsonObject? layer, string source)
        {
            if (layer is null)
            {
                return;
            }

            foreach (var (name, value) in layer)
            {
                if (definition.FindProperty(name) is { } property && property.Accepts(value))
                {
                    values[property.Name] = value?.DeepClone();
                    sources[property.Name] = source;
                }
            }
        }
    }
}