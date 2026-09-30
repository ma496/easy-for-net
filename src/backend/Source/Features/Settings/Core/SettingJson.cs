namespace Backend.Features.Settings.Core;

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

/// <summary>
/// The one JSON shape settings are stored, merged and served in.
/// </summary>
/// <remarks>
/// Property names are written camelCase (<c>isEmailVerificationRequired</c>) - in the stored
/// <c>SettingValues.Values</c> objects, in the API's responses, and as the keys a write names - and are
/// matched case-insensitively when read, so a stored or submitted <c>IsEmailVerificationRequired</c>
/// still lands on its property and is written back camelCase. Enums are written as camelCase strings
/// and read from either their name or their number. Merging is per top-level property: a property whose
/// value is itself an object is overridden as a whole, never merged member by member.
/// </remarks>
static class SettingJson
{
    /// <summary>The serializer options every settings read and write goes through.</summary>
    public static readonly JsonSerializerOptions Options = CreateOptions();

    /// <summary>
    /// Parses a stored overrides document, or answers <see langword="null"/> when there is none or it
    /// is not a JSON object - a row that cannot contribute a property contributes nothing rather than
    /// failing every read of the setting.
    /// </summary>
    /// <param name="values">The stored <c>jsonb</c> text.</param>
    public static JsonObject? ParseObject(string? values)
    {
        if (string.IsNullOrWhiteSpace(values))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(values) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver()
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        options.MakeReadOnly();
        return options;
    }
}