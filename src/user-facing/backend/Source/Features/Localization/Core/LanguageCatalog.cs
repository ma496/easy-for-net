namespace Backend.Features.Localization.Core;

/// <summary>
/// Fixed metadata about every culture the API is built to ship translations for - its English display
/// name and whether it is written right-to-left. This is not any scope's enabled set (see
/// <see cref="ILocalizationService"/> for that); it is the catalog an enabled set draws its display
/// information from. A shipped resource file with no entry here is a configuration mistake the
/// shipped-consistency tests catch, not a runtime condition this type has to handle gracefully.
/// </summary>
public static class LanguageCatalog
{
    private static readonly Dictionary<string, (string Name, bool IsRtl)> Entries = new(StringComparer.OrdinalIgnoreCase)
    {
        ["en"] = ("English", false),
        ["ar"] = ("Arabic", true),
        ["ur"] = ("Urdu", true),
        ["zh"] = ("Chinese", false),
        ["es"] = ("Spanish", false),
        ["fr"] = ("French", false),
        ["hi"] = ("Hindi", false),
        ["ru"] = ("Russian", false),
    };

    /// <summary>Every culture code the catalog carries metadata for.</summary>
    public static IReadOnlyCollection<string> Codes => Entries.Keys;

    /// <summary>Whether the catalog has an entry for <paramref name="code"/>.</summary>
    public static bool Contains(string code) => Entries.ContainsKey(code);

    /// <summary>
    /// Looks up the English display name and right-to-left flag for a culture code, or
    /// <see langword="null"/> when the catalog carries no entry for it.
    /// </summary>
    public static (string Name, bool IsRtl)? TryGet(string code)
        => Entries.TryGetValue(code, out var entry) ? entry : null;
}
