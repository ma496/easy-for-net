namespace Backend.Features.Settings.Core;

using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.DataProtection;

/// <summary>
/// Resolves settings layer by layer and writes the acting scope's overrides. Internal to the
/// <c>Settings</c> slice: <see cref="ISettingProvider"/> is the published, typed face of the same
/// resolution, and the slice's endpoints use this for the per-property detail and for writes.
/// </summary>
/// <remarks>
/// A value resolves property by property, first layer with an answer winning: the tenant's own
/// override, then the platform's (<c>TenantId</c> <see langword="null"/>), then the default - the
/// configured default when the setting names a configuration section the deployment supplies, else the
/// setting class's property initializers. Resolving for no tenant is the platform's answer:
/// default with the platform's overrides laid over it. A stored property the class no longer has, or
/// one whose stored value no longer deserializes to the property's type, contributes nothing and the
/// property follows the layer below.
/// <para>
/// A <see cref="SecretSettingAttribute"/> property is stored encrypted with ASP.NET Data Protection and
/// decrypted as its row is read, so everything this service answers holds the plaintext - which is why
/// the endpoints mask it (<see cref="SettingDto.From"/>) before it leaves the API. A stored secret that
/// cannot be decrypted - the key ring was lost or rotated out, the row was written by hand - is logged
/// and dropped from its layer, so the property follows the layer below instead of failing the read.
/// A secret bound to other properties (<see cref="SecretSettingAttribute.BoundTo"/>) resolves to an
/// empty string whenever one of them is answered by a higher layer than the secret is.
/// </para>
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

    /// <summary>
    /// One scope's own overrides of one setting, secrets decrypted: <paramref name="tenantId"/>'s row
    /// when a tenant is named, the platform's when none is - an empty object when the scope has no row.
    /// </summary>
    Task<JsonObject> GetOwnAsync(SettingDefinition definition, Guid? tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the acting scope's overrides of one setting with <paramref name="ownOverrides"/>, given in
    /// plaintext - each secret property is encrypted before it is stored; an empty object removes them.
    /// </summary>
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
    /// <summary>The setting's default: the configured default when its section is supplied, else the code default.</summary>
    public const string Default = "default";
}

/// <summary>Default implementation of <see cref="ISettingValueService"/>.</summary>
[NoDirectUse]
class SettingValueService(ISettingValueStore store, IDataProtectionProvider dataProtectionProvider, ILogger<SettingValueService> logger)
    : ISettingValueService
{
    /// <summary>
    /// The Data Protection purpose secret setting values are encrypted under, narrowed by the setting's
    /// name and the property's JSON name - so a ciphertext copied into another setting or property does
    /// not decrypt there.
    /// </summary>
    internal const string SecretProtectionPurpose = "Backend.Settings.Secrets";

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
    public async Task<JsonObject> GetOwnAsync(SettingDefinition definition, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        var (platform, tenant) = await GetLayersAsync(definition, tenantId, cancellationToken);
        var own = tenantId is null ? platform : tenant;
        return own is null ? [] : (JsonObject)own.DeepClone();
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
            await store.SetOwnAsync(definition.Name, Protect(definition, ownOverrides).ToJsonString(SettingJson.Options), cancellationToken);
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
        var layers = (
            Unprotect(definition, SettingJson.ParseObject(rows.PlatformValues), SettingValueSource.Platform, null),
            Unprotect(definition, SettingJson.ParseObject(rows.TenantValues), SettingValueSource.Tenant, tenantId));
        _layers[key] = layers;
        return layers;
    }

    /// <summary>
    /// A copy of <paramref name="ownOverrides"/> with every secret string it holds encrypted, ready to
    /// store. A secret whose value is not a string is copied as it is; a read drops it.
    /// </summary>
    private JsonObject Protect(SettingDefinition definition, JsonObject ownOverrides)
    {
        var stored = (JsonObject)ownOverrides.DeepClone();
        foreach (var (name, value) in ownOverrides)
        {
            if (definition.FindProperty(name) is { IsSecret: true } property && TryReadString(value, out var plain))
            {
                stored[name] = ProtectorFor(definition, property).Protect(plain);
            }
        }

        return stored;
    }

    /// <summary>
    /// Decrypts, in place, every secret a parsed stored layer holds. One that is not a string or does not
    /// decrypt is removed from the layer - so the property follows the layer below - and logged by the
    /// setting, property and layer it sits in, never by its value.
    /// </summary>
    private JsonObject? Unprotect(SettingDefinition definition, JsonObject? layer, string layerName, Guid? tenantId)
    {
        if (layer is null)
        {
            return null;
        }

        foreach (var (name, value) in layer.ToList())
        {
            if (definition.FindProperty(name) is not { IsSecret: true } property)
            {
                continue;
            }

            string? plain = null;
            try
            {
                if (TryReadString(value, out var cipher))
                {
                    plain = ProtectorFor(definition, property).Unprotect(cipher);
                }
            }
            catch (Exception exception) when (exception is CryptographicException or FormatException)
            {
                plain = null;
            }

            if (plain is null)
            {
                logger.LogWarning(
                    "The stored secret {Property} of the setting {Setting} in the {Layer} layer (tenant {TenantId}) could not be decrypted and is ignored; the layer below answers for it.",
                    property.Name,
                    definition.Name,
                    layerName,
                    tenantId);
                layer.Remove(name);
            }
            else
            {
                layer[name] = plain;
            }
        }

        return layer;
    }

    /// <summary>The protector for one secret property of one setting.</summary>
    private IDataProtector ProtectorFor(SettingDefinition definition, SettingPropertyDefinition property)
        => dataProtectionProvider.CreateProtector(SecretProtectionPurpose, definition.Name, property.Name);

    /// <summary>Reads <paramref name="value"/> as a string, when it is a JSON string.</summary>
    private static bool TryReadString(JsonNode? value, out string text)
    {
        if (value is JsonValue jsonValue && jsonValue.GetValueKind() == JsonValueKind.String)
        {
            text = jsonValue.GetValue<string>();
            return true;
        }

        text = null!;
        return false;
    }

    /// <summary>
    /// Lays the platform layer and then the tenant layer over a fresh copy of the default. The
    /// layers are never modified - values are copied out of them - so a cached layer serves every read.
    /// </summary>
    private static ResolvedSetting Merge(SettingDefinition definition, JsonObject? platform, JsonObject? tenant)
    {
        var values = definition.CreateDefaultValues();
        var sources = definition.Properties.ToDictionary(property => property.Name, _ => SettingValueSource.Default, StringComparer.Ordinal);

        Lay(platform, SettingValueSource.Platform);
        Lay(tenant, SettingValueSource.Tenant);
        WithholdUnboundSecrets(definition, values, sources);

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

    /// <summary>
    /// Enforces that a secret is never inherited apart from the properties that decide where it is sent:
    /// a secret whose bound property was answered by a higher layer than the secret itself resolves to an
    /// empty string, reported as coming from that higher layer - the layer that took over where it goes
    /// and did not supply a secret for it.
    /// </summary>
    private static void WithholdUnboundSecrets(SettingDefinition definition, JsonObject values, Dictionary<string, string> sources)
    {
        foreach (var secret in definition.Properties.Where(property => property.IsSecret && property.BoundProperties.Count > 0))
        {
            var secretRank = Rank(sources[secret.Name]);
            var highestBound = secret.BoundProperties.MaxBy(bound => Rank(sources[bound]))!;
            if (Rank(sources[highestBound]) > secretRank)
            {
                values[secret.Name] = string.Empty;
                sources[secret.Name] = sources[highestBound];
            }
        }

        static int Rank(string source) => source switch
        {
            SettingValueSource.Tenant => 2,
            SettingValueSource.Platform => 1,
            _ => 0
        };
    }
}