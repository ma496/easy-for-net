namespace Backend.Features.Localization.Core;

/// <summary>
/// The cultures this build ships resources for - what another slice validates a culture code against.
/// A singleton over the embedded resource files, so a validator may resolve it.
/// </summary>
[AllowOutside]
public interface ICultureCatalog
{
    /// <summary>The culture codes shipped, in their canonical spelling.</summary>
    IReadOnlyCollection<string> ShippedCultures { get; }

    /// <summary>
    /// The canonical shipped spelling of <paramref name="culture"/>, matched case-insensitively, or
    /// <see langword="null"/> when it is not shipped.
    /// </summary>
    /// <param name="culture">The culture code to look up.</param>
    string? TryCanonicalize(string culture);
}

/// <summary>
/// The languages the application currently offers - the administrator's language settings resolved
/// against what is shipped. Scoped, since it reads the database.
/// </summary>
[AllowOutside]
public interface ILanguageProvider
{
    /// <summary>Resolves the enabled cultures and the culture served when none is asked for.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    Task<EnabledLanguages> GetEnabledLanguagesAsync(CancellationToken cancellationToken = default);
}

/// <summary>The cultures the application offers, and the one it serves by default.</summary>
/// <param name="Cultures">Every enabled culture; never empty.</param>
/// <param name="DefaultCulture">
/// The culture served when none is asked for: the configured default, else English when enabled, else
/// the first enabled culture.
/// </param>
[AllowOutside]
public sealed record EnabledLanguages(IReadOnlyList<string> Cultures, string DefaultCulture);

/// <summary>Default <see cref="ICultureCatalog"/>, answered from the shipped resource files.</summary>
[NoDirectUse]
internal sealed class CultureCatalog(ILocalizationResourceStore resourceStore) : ICultureCatalog
{
    /// <inheritdoc />
    public IReadOnlyCollection<string> ShippedCultures => resourceStore.ShippedCultures;

    /// <inheritdoc />
    public string? TryCanonicalize(string culture) => resourceStore.TryCanonicalize(culture);
}

/// <summary>Default <see cref="ILanguageProvider"/>, a thin adapter over <see cref="ILocalizationService"/>.</summary>
[NoDirectUse]
internal sealed class LanguageProvider(ILocalizationService localizationService) : ILanguageProvider
{
    /// <inheritdoc />
    public async Task<EnabledLanguages> GetEnabledLanguagesAsync(CancellationToken cancellationToken = default)
    {
        var languages = await localizationService.ResolveLanguagesAsync(cancellationToken);
        var cultures = languages.EnabledCultures;
        var defaultCulture = languages.DefaultCulture is { } configured && cultures.Contains(configured)
            ? configured
            : cultures.FirstOrDefault(culture => string.Equals(culture, "en", StringComparison.OrdinalIgnoreCase)) ?? cultures[0];

        return new EnabledLanguages(cultures, defaultCulture);
    }
}
