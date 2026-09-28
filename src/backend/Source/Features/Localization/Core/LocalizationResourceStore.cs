namespace Backend.Features.Localization.Core;

using System.Reflection;
using System.Text.Json;

/// <summary>
/// Loads the locale JSON files embedded in the assembly exactly once and answers every read against
/// that in-memory snapshot for the lifetime of the process - the shipped resources never change
/// without a redeploy, so there is nothing to invalidate. Each culture's nested JSON object is
/// flattened to dotted keys (<c>common.save</c>), which is the shape both the resolution service and
/// the admin editor work with.
/// </summary>
public interface ILocalizationResourceStore
{
    /// <summary>The culture codes actually shipped - one per embedded resource file found.</summary>
    IReadOnlyCollection<string> ShippedCultures { get; }

    /// <summary>
    /// The flattened dotted keys and shipped values for <paramref name="culture"/>, or
    /// <see langword="null"/> when that culture is not shipped.
    /// </summary>
    IReadOnlyDictionary<string, string>? GetResources(string culture);

    /// <summary>
    /// The shipped English resource. It declares the whole key universe every other shipped locale
    /// mirrors, and is the fallback value for a key a requested culture happens to be missing.
    /// </summary>
    IReadOnlyDictionary<string, string> EnglishResources { get; }

    /// <summary>
    /// Resolves the canonical shipped code for <paramref name="culture"/> by a case-insensitive match
    /// against <see cref="ShippedCultures"/> - <c>"EN"</c> and <c>"en"</c> resolve to the same stored
    /// culture - or <see langword="null"/> when <paramref name="culture"/> is not shipped at all. This
    /// is the one place an incoming culture is mapped to the form it is validated, stored, queried and
    /// compared against, so a caller's casing never fragments the same culture into several rows.
    /// </summary>
    string? TryCanonicalize(string culture);
}

/// <summary>
/// Default <see cref="ILocalizationResourceStore"/>, backed by the JSON files embedded from
/// <c>Features/Localization/Core/Resources</c>.
/// </summary>
[NoDirectUse]
public sealed class LocalizationResourceStore : ILocalizationResourceStore
{
    private const string EnglishCulture = "en";

    /// <summary>
    /// The manifest resource name prefix every shipped locale file is embedded under - the fixed
    /// <c>LogicalName</c> the project file gives them, so it does not follow the root namespace.
    /// </summary>
    private const string ResourceNamespace = "Localization.Resources.";

    private readonly Dictionary<string, IReadOnlyDictionary<string, string>> _resourcesByCulture;

    public LocalizationResourceStore()
    {
        _resourcesByCulture = LoadAll();

        if (!_resourcesByCulture.ContainsKey(EnglishCulture))
        {
            throw new InvalidOperationException(
                $"No embedded '{EnglishCulture}.json' locale resource was found under '{ResourceNamespace}'. English is the key set every other locale and every override is checked against, so it must always ship.");
        }
    }

    /// <inheritdoc />
    public IReadOnlyCollection<string> ShippedCultures => _resourcesByCulture.Keys;

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string>? GetResources(string culture)
        => _resourcesByCulture.TryGetValue(culture, out var resources) ? resources : null;

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> EnglishResources => _resourcesByCulture[EnglishCulture];

    /// <inheritdoc />
    public string? TryCanonicalize(string culture)
        => _resourcesByCulture.Keys.FirstOrDefault(shipped => string.Equals(shipped, culture, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Reads every embedded locale JSON file off the executing assembly's manifest and flattens each
    /// one into a dotted-key dictionary, keyed by the culture code taken from the file name.
    /// </summary>
    private static Dictionary<string, IReadOnlyDictionary<string, string>> LoadAll()
    {
        var assembly = typeof(LocalizationResourceStore).Assembly;
        var result = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        var resourceNames = assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(ResourceNamespace, StringComparison.Ordinal)
                           && name.EndsWith(".json", StringComparison.Ordinal));

        foreach (var resourceName in resourceNames)
        {
            var culture = resourceName[ResourceNamespace.Length..^".json".Length];

            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"The embedded locale resource '{resourceName}' could not be opened.");
            using var document = JsonDocument.Parse(stream);

            var flattened = new Dictionary<string, string>(StringComparer.Ordinal);
            Flatten(document.RootElement, prefix: null, flattened);
            result[culture] = flattened;
        }

        return result;
    }

    /// <summary>
    /// Recursively flattens a nested JSON object into <paramref name="destination"/>, joining each
    /// level's property name with a dot (<c>common.save</c>).
    /// </summary>
    private static void Flatten(JsonElement element, string? prefix, Dictionary<string, string> destination)
    {
        foreach (var property in element.EnumerateObject())
        {
            var key = prefix is null ? property.Name : $"{prefix}.{property.Name}";

            if (property.Value.ValueKind == JsonValueKind.Object)
            {
                Flatten(property.Value, key, destination);
            }
            else
            {
                destination[key] = property.Value.GetString() ?? string.Empty;
            }
        }
    }
}
