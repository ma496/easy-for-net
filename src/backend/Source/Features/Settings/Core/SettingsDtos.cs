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

    /// <summary>Projects a resolution into the shape the API answers in.</summary>
    internal static SettingDto From(ResolvedSetting resolved) => new()
    {
        Name = resolved.Definition.Name,
        Properties =
        [
            .. resolved.Definition.Properties.Select(property => new SettingPropertyDto
            {
                Name = property.Name,
                Value = resolved.Values[property.Name]?.DeepClone(),
                Source = resolved.Sources[property.Name]
            })
        ]
    };
}

/// <summary>One value of a resolved setting and the layer it came from.</summary>
public sealed class SettingPropertyDto
{
    /// <summary>The property's camelCase JSON name.</summary>
    public string Name { get; set; } = null!;

    /// <summary>The effective value, as JSON.</summary>
    public JsonNode? Value { get; set; }

    /// <summary>
    /// Which layer answered: <c>tenant</c> (the acting tenant's own override), <c>platform</c> (the
    /// platform's override) or <c>default</c> (the setting class's code default).
    /// </summary>
    public string Source { get; set; } = null!;
}