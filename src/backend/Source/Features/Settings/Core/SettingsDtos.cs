namespace Backend.Features.Settings.Core;

using System.Text.Json.Nodes;

/// <summary>
/// One setting as the acting scope resolves it, property by property - what <c>GET /settings</c> lists
/// and what <c>PUT /settings/{name}</c> answers with.
/// </summary>
public sealed class SettingDto
{
    /// <summary>The setting's registered name.</summary>
    public string Name { get; set; } = null!;

    /// <summary>The setting's values, in the order the setting class declares them.</summary>
    public List<SettingPropertyDto> Properties { get; set; } = [];

    /// <summary>
    /// Projects a resolution into the shape the API answers in. A secret property's value never leaves
    /// the API: it is answered with no value and only whether one is set, whichever layer holds it.
    /// </summary>
    internal static SettingDto From(ResolvedSetting resolved) => new()
    {
        Name = resolved.Definition.Name,
        Properties =
        [
            .. resolved.Definition.Properties.Select(property =>
            {
                var value = resolved.Values[property.Name];
                return new SettingPropertyDto
                {
                    Name = property.Name,
                    Value = property.IsSecret ? null : value?.DeepClone(),
                    Source = resolved.Sources[property.Name],
                    IsSecret = property.IsSecret,
                    IsSet = property.IsSecret ? IsNonEmptyString(value) : null
                };
            })
        ]
    };

    private static bool IsNonEmptyString(JsonNode? value)
        => value is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var text) && text.Length > 0;
}

/// <summary>One value of a resolved setting and the layer it came from.</summary>
public sealed class SettingPropertyDto
{
    /// <summary>The property's camelCase JSON name.</summary>
    public string Name { get; set; } = null!;

    /// <summary>The effective value, as JSON - always <see langword="null"/> for a secret.</summary>
    public JsonNode? Value { get; set; }

    /// <summary>
    /// Which layer answered: <c>tenant</c> (the acting tenant's own override), <c>platform</c> (the
    /// platform's override) or <c>default</c> (the setting's default - its configured default when the
    /// deployment supplies its configuration section, else the setting class's code default).
    /// </summary>
    public string Source { get; set; } = null!;

    /// <summary>Whether the property is a secret, whose value the API never returns.</summary>
    public bool IsSecret { get; set; }

    /// <summary>
    /// For a secret, whether its effective value is a non-empty string; <see langword="null"/> for any
    /// other property, whose <see cref="Value"/> speaks for itself.
    /// </summary>
    public bool? IsSet { get; set; }
}
