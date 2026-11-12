using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.DataProtection;

namespace Backend.Features.Settings.Core;

/// <summary>
/// Resolves settings and writes their stored overrides. Internal to the
/// <c>Settings</c> slice: <see cref="ISettingProvider"/> is the published, typed face of the same
/// resolution, and the slice's endpoints use this for the per-property detail and for writes.
/// </summary>
/// <remarks>
/// A value resolves property by property: the stored override when it holds the property, else the
/// default - the configured default when the setting names a configuration section the deployment
/// supplies, else the setting class's property initializers. A stored property the class no longer has,
/// or one whose stored value no longer deserializes to the property's type, contributes nothing and the
/// property follows the default.
/// <para>
/// A <see cref="SecretSettingAttribute"/> property is stored encrypted with ASP.NET Data Protection and
/// decrypted as its row is read, so everything this service answers holds the plaintext - which is why
/// the endpoints mask it (<see cref="SettingDto.From"/>) before it leaves the API. A stored secret that
/// cannot be decrypted - the key ring was lost or rotated out, the row was written by hand - is logged
/// and dropped, so the property follows the default instead of failing the read.
/// A secret bound to other properties (<see cref="SecretSettingAttribute.BoundTo"/>) resolves to an
/// empty string whenever one of them is overridden while the secret is not.
/// </para>
/// <para>
/// Stored rows are read once per setting for the lifetime of this instance - one request or
/// job, since the service is registered scoped - and a write through this instance drops what it had
/// read for that setting, so a read after a write in the same scope sees the write.
/// </para>
/// </remarks>
interface ISettingValueService
{
    /// <summary>Resolves one setting.</summary>
    Task<ResolvedSetting> ResolveAsync(SettingDefinition definition, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves one setting as it would stand if its stored overrides were replaced by
    /// <paramref name="ownOverrides"/>. Nothing is written.
    /// </summary>
    ResolvedSetting Preview(SettingDefinition definition, JsonObject ownOverrides);

    /// <summary>
    /// The stored overrides of one setting, secrets decrypted - an empty object when there is no row.
    /// </summary>
    Task<JsonObject> GetOwnAsync(SettingDefinition definition, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the stored overrides of one setting with <paramref name="ownOverrides"/>, given in
    /// plaintext - each secret property is encrypted before it is stored; an empty object removes them.
    /// </summary>
    Task SetOwnAsync(SettingDefinition definition, JsonObject ownOverrides, CancellationToken cancellationToken = default);

    /// <summary>Removes the stored overrides of one setting, if it has any. Idempotent.</summary>
    Task DeleteOwnAsync(SettingDefinition definition, CancellationToken cancellationToken = default);
}

/// <summary>One setting resolved: the merged values and where each property's value came from.</summary>
/// <param name="Definition">The setting resolved.</param>
/// <param name="Values">The merged values, one member per property, under their JSON names.</param>
/// <param name="Sources">Per property JSON name, the layer that answered it - a <see cref="SettingValueSource"/> constant.</param>
sealed record ResolvedSetting(SettingDefinition Definition, JsonObject Values, IReadOnlyDictionary<string, string> Sources);

/// <summary>The layer a resolved property's value came from, as the API reports it.</summary>
static class SettingValueSource
{
    /// <summary>The stored override.</summary>
    public const string Override = "override";
    /// <summary>The setting's default: the configured default when its section is supplied, else the code default.</summary>
    public const string Default = "default";
}

/// <summary>Default implementation of <see cref="ISettingValueService"/>.</summary>
[NoDirectUse]
class SettingValueService(ISettingValueStore store,
                           IDataProtectionProvider dataProtectionProvider,
                           IEnumerable<ISettingChangedHandler> changedHandlers,
                           ILogger<SettingValueService> logger)
    : ISettingValueService
{
    /// <summary>
    /// The Data Protection purpose secret setting values are encrypted under, narrowed by the setting's
    /// name and the property's JSON name - so a ciphertext copied into another setting or property does
    /// not decrypt there.
    /// </summary>
    internal const string SecretProtectionPurpose = "Backend.Settings.Secrets";

    /// <summary>The parsed stored overrides, keyed by the setting's name.</summary>
    private readonly Dictionary<string, JsonObject?> _layers = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public async Task<ResolvedSetting> ResolveAsync(SettingDefinition definition, CancellationToken cancellationToken = default)
        => Merge(definition, await GetLayerAsync(definition, cancellationToken));

    /// <inheritdoc />
    public ResolvedSetting Preview(SettingDefinition definition, JsonObject ownOverrides)
        => Merge(definition, ownOverrides);

    /// <inheritdoc />
    public async Task<JsonObject> GetOwnAsync(SettingDefinition definition, CancellationToken cancellationToken = default)
    {
        var own = await GetLayerAsync(definition, cancellationToken);
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

        _layers.Remove(definition.Name);
        NotifyChanged(definition);
    }

    /// <inheritdoc />
    public async Task DeleteOwnAsync(SettingDefinition definition, CancellationToken cancellationToken = default)
    {
        await store.DeleteOwnAsync(definition.Name, cancellationToken);
        _layers.Remove(definition.Name);
        NotifyChanged(definition);
    }

    /// <summary>Tells every registered <see cref="ISettingChangedHandler"/> that a setting changed.</summary>
    private void NotifyChanged(SettingDefinition definition)
    {
        foreach (var handler in changedHandlers)
        {
            handler.OnSettingChanged(definition.Name);
        }
    }

    private async Task<JsonObject?> GetLayerAsync(SettingDefinition definition, CancellationToken cancellationToken)
    {
        if (_layers.TryGetValue(definition.Name, out var cached))
        {
            return cached;
        }

        var row = await store.GetValuesAsync(definition.Name, cancellationToken);
        var layer = Unprotect(definition, SettingJson.ParseObject(row));
        _layers[definition.Name] = layer;
        return layer;
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
    /// Decrypts, in place, every secret the parsed stored overrides hold. One that is not a string or does
    /// not decrypt is removed - so the property follows the default - and logged by the setting and
    /// property it sits in, never by its value.
    /// </summary>
    private JsonObject? Unprotect(SettingDefinition definition, JsonObject? layer)
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
                    "The stored secret {Property} of the setting {Setting} could not be decrypted and is ignored; the default answers for it.",
                    property.Name,
                    definition.Name);
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
    /// Lays the stored overrides over a fresh copy of the default. The overrides are never modified -
    /// values are copied out of them - so a cached layer serves every read.
    /// </summary>
    private static ResolvedSetting Merge(SettingDefinition definition, JsonObject? overrides)
    {
        var values = definition.CreateDefaultValues();
        var sources = definition.Properties.ToDictionary(property => property.Name, _ => SettingValueSource.Default, StringComparer.Ordinal);

        Lay(overrides, SettingValueSource.Override);
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
    /// a secret whose bound property is overridden while the secret itself is not resolves to an empty
    /// string, reported as coming from the override - which took over where it goes and did not supply a
    /// secret for it.
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
            SettingValueSource.Override => 1,
            _ => 0
        };
    }
}