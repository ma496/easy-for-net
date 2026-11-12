using Backend.Features.Localization.Core.Entities;

namespace Backend.Features.Localization.Core;

/// <summary>
/// Resolves the translation resources and language settings the application serves, and administers
/// the overrides an administrator edits from the admin screen. Internal to the <c>Localization</c>
/// feature: the global error plumbing that needs to look up a business error's message reaches this
/// through the narrow, published <see cref="IErrorMessageLocalizer"/> instead of depending on this
/// interface directly - see that type for the rationale.
/// </summary>
/// <remarks>
/// A text key resolves first answer wins: the stored override, then the shipped value for the
/// requested culture, then the shipped English value.
/// <para>
/// Which cultures are offered, and which of them is the default, resolve as a unit: the stored
/// <see cref="LanguageSetting"/> row if there is one, else every shipped culture enabled with no
/// configured default.
/// </para>
/// </remarks>
internal interface ILocalizationService
{
    /// <summary>
    /// Resolves the resources served for a request naming <paramref name="requestedCulture"/>: the
    /// culture actually served (the requested one if enabled, else the default, else English if
    /// enabled, else the first enabled culture), the enabled languages, and the merged resource
    /// dictionary for the served culture.
    /// </summary>
    Task<ResolvedResources> ResolveResourcesAsync(string requestedCulture, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the localized message for a business error carrying <paramref name="errorCode"/>, or
    /// <see langword="null"/> when the served culture (the same resolution <see cref="ResolveResourcesAsync"/>
    /// documents) has no <c>error.server.&lt;code&gt;</c> key at all - which is how a plain
    /// FluentValidation rule (<c>NotEmptyValidator</c>, with no <see cref="ErrorCodes"/> constant behind
    /// it) is told apart from a business error, and left exactly as FluentValidation wrote it.
    /// </summary>
    /// <param name="requestedCulture">The culture negotiated for the request (from its <c>Accept-Language</c> header).</param>
    /// <param name="errorCode">The <see cref="ErrorCodes"/> value the failure carries.</param>
    /// <param name="propertyName">
    /// The request property the error belongs to, exactly as FastEndpoints reports it - camelCased,
    /// empty for a request-level error. A message containing <c>${propertyName}</c> has it replaced
    /// with that field's own localized label: a resource key matching the field name with a trailing
    /// "Normalized" suffix stripped and its first letter lowercased, falling back to the raw field name
    /// when no such label is shipped.
    /// </param>
    /// <param name="cancellationToken">Cancels the resource resolution.</param>
    Task<string?> TryLocalizeErrorAsync(string requestedCulture, string errorCode, string propertyName, CancellationToken cancellationToken = default);

    /// <summary>Resolves the effective and shipped language settings.</summary>
    Task<LanguageResolution> ResolveLanguagesAsync(CancellationToken cancellationToken = default);

    /// <summary>Builds one page of the text-override editor for one culture.</summary>
    Task<LocalizationTextPage> GetTextsAsync(
        string culture, string? filter, bool onlyOverridden, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>Upserts the override for one key.</summary>
    Task SetTextAsync(string culture, string key, string value, CancellationToken cancellationToken = default);

    /// <summary>Removes the override for one key, if there is one. Idempotent.</summary>
    Task DeleteTextAsync(string culture, string key, CancellationToken cancellationToken = default);

    /// <summary>Upserts the language settings row.</summary>
    Task SetLanguagesAsync(IReadOnlyList<string> enabledCultures, string? defaultCulture, CancellationToken cancellationToken = default);

    /// <summary>Removes the language settings row, if there is one. Idempotent.</summary>
    Task DeleteLanguagesAsync(CancellationToken cancellationToken = default);
}

/// <summary>EF Core-backed implementation of <see cref="ILocalizationService"/>.</summary>
[NoDirectUse]
internal class LocalizationService(AppDbContext dbContext, ILocalizationResourceStore resourceStore)
    : ILocalizationService
{
    /// <summary>
    /// Memoizes a resolution by its canonicalized requested culture, for the lifetime of this instance -
    /// one request or job, since the service is registered scoped. Cleared by every write.
    /// </summary>
    private readonly Dictionary<string, ResolvedResources> _resolvedByCulture = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public async Task<ResolvedResources> ResolveResourcesAsync(string requestedCulture, CancellationToken cancellationToken = default)
    {
        requestedCulture = Canonicalize(requestedCulture);
        if (_resolvedByCulture.TryGetValue(requestedCulture, out var cached))
        {
            return cached;
        }

        var state = await ResolveLanguageStateAsync(cancellationToken);
        var servedCulture = ResolveServedCulture(requestedCulture, state.EnabledCultures, state.DefaultCulture);

        var overrides = await dbContext.LocalizationTexts
            .AsNoTracking()
            .Where(x => x.Culture == servedCulture)
            .Select(x => new { x.Key, x.Value })
            .ToListAsync(cancellationToken);

        var resources = new Dictionary<string, string>(resourceStore.EnglishResources, StringComparer.Ordinal);
        if (resourceStore.GetResources(servedCulture) is { } shippedForCulture)
        {
            foreach (var (key, value) in shippedForCulture)
            {
                resources[key] = value;
            }
        }

        foreach (var text in overrides)
        {
            resources[text.Key] = text.Value;
        }

        var result = new ResolvedResources
        {
            Culture = servedCulture,
            DefaultCulture = state.DefaultCulture,
            Languages = [.. state.EnabledCultures.Select(ToLanguageDto)],
            Resources = resources,
        };
        _resolvedByCulture[requestedCulture] = result;

        return result;
    }

    /// <inheritdoc />
    public async Task<string?> TryLocalizeErrorAsync(string requestedCulture, string errorCode, string propertyName, CancellationToken cancellationToken = default)
    {
        var resolved = await ResolveResourcesAsync(requestedCulture, cancellationToken);
        var key = $"error.server.{errorCode}";

        if (!resolved.Resources.TryGetValue(key, out var message))
        {
            return null;
        }

        const string placeholder = "${propertyName}";
        if (message.Contains(placeholder, StringComparison.Ordinal))
        {
            var fieldName = ToFieldName(propertyName);
            var label = resolved.Resources.TryGetValue(fieldName, out var shippedLabel) ? shippedLabel : fieldName;
            message = message.Replace(placeholder, label, StringComparison.Ordinal);
        }

        return message;
    }

    /// <summary>
    /// Maps a request property name to its form-field label key: a trailing "Normalized" suffix
    /// stripped, and the first character lowercased ("EmailNormalized" -> "email", "FirstName" ->
    /// "firstName", "" -> "").
    /// </summary>
    private static string ToFieldName(string propertyName)
    {
        const string normalizedSuffix = "Normalized";
        var stripped = propertyName.EndsWith(normalizedSuffix, StringComparison.OrdinalIgnoreCase)
            ? propertyName[..^normalizedSuffix.Length]
            : propertyName;

        return stripped.Length == 0 ? stripped : char.ToLowerInvariant(stripped[0]) + stripped[1..];
    }

    /// <inheritdoc />
    public async Task<LanguageResolution> ResolveLanguagesAsync(CancellationToken cancellationToken = default)
    {
        var state = await ResolveLanguageStateAsync(cancellationToken);
        var shippedCultures = resourceStore.ShippedCultures.ToList();

        return new LanguageResolution
        {
            Languages = [.. resourceStore.ShippedCultures.OrderBy(code => code, StringComparer.Ordinal).Select(ToLanguageDto)],
            EnabledCultures = state.EnabledCultures,
            DefaultCulture = state.DefaultCulture,
            IsInherited = !state.HasOwnRow,
            InheritedEnabledCultures = shippedCultures,
            InheritedDefaultCulture = null,
        };
    }

    /// <inheritdoc />
    public async Task<LocalizationTextPage> GetTextsAsync(
        string culture, string? filter, bool onlyOverridden, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        culture = Canonicalize(culture);

        var overrides = await dbContext.LocalizationTexts
            .AsNoTracking()
            .Where(x => x.Culture == culture)
            .Select(x => new { x.Key, x.Value })
            .ToDictionaryAsync(x => x.Key, x => x.Value, StringComparer.Ordinal, cancellationToken);

        var englishResources = resourceStore.EnglishResources;
        var shippedForCulture = resourceStore.GetResources(culture) ?? englishResources;

        var rows = new List<LocalizationTextRow>(englishResources.Count);
        foreach (var key in englishResources.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            var defaultValue = shippedForCulture.TryGetValue(key, out var shippedValue) ? shippedValue : englishResources[key];

            rows.Add(new LocalizationTextRow
            {
                Key = key,
                DefaultValue = defaultValue,
                InheritedValue = defaultValue,
                Value = overrides.TryGetValue(key, out var ownValue) ? ownValue : null,
            });
        }

        if (onlyOverridden)
        {
            rows = [.. rows.Where(r => r.Value is not null)];
        }

        if (!string.IsNullOrWhiteSpace(filter))
        {
            var needle = filter.Trim();
            rows = [.. rows.Where(r =>
                r.Key.Contains(needle, StringComparison.OrdinalIgnoreCase)
                || r.DefaultValue.Contains(needle, StringComparison.OrdinalIgnoreCase)
                || (r.Value ?? r.InheritedValue).Contains(needle, StringComparison.OrdinalIgnoreCase))];
        }

        var total = rows.Count;
        var items = rows.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new LocalizationTextPage { Items = items, Total = total };
    }

    /// <inheritdoc />
    public async Task SetTextAsync(string culture, string key, string value, CancellationToken cancellationToken = default)
    {
        culture = Canonicalize(culture);

        var entity = await dbContext.LocalizationTexts
            .FirstOrDefaultAsync(x => x.Culture == culture && x.Key == key, cancellationToken);

        if (entity is null)
        {
            entity = new LocalizationText { Culture = culture, Key = key, Value = value };
            dbContext.LocalizationTexts.Add(entity);
        }
        else
        {
            entity.Value = value;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        _resolvedByCulture.Clear();
    }

    /// <inheritdoc />
    public async Task DeleteTextAsync(string culture, string key, CancellationToken cancellationToken = default)
    {
        culture = Canonicalize(culture);

        var entity = await dbContext.LocalizationTexts
            .FirstOrDefaultAsync(x => x.Culture == culture && x.Key == key, cancellationToken);

        if (entity is null)
        {
            return;
        }

        dbContext.LocalizationTexts.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        _resolvedByCulture.Clear();
    }

    /// <inheritdoc />
    public async Task SetLanguagesAsync(IReadOnlyList<string> enabledCultures, string? defaultCulture, CancellationToken cancellationToken = default)
    {
        // Canonicalized before storage so "FR" and "fr" land as the same stored culture rather than
        // fragmenting it, exactly as a text override's culture does.
        var canonicalEnabled = enabledCultures.Select(Canonicalize).Distinct(StringComparer.Ordinal).ToList();
        var canonicalDefault = defaultCulture is not null ? Canonicalize(defaultCulture) : null;

        var entity = await dbContext.LanguageSettings.OrderBy(x => x.Id).FirstOrDefaultAsync(cancellationToken);

        if (entity is null)
        {
            entity = new LanguageSetting();
            dbContext.LanguageSettings.Add(entity);
        }

        entity.EnabledCultures = canonicalEnabled;
        entity.DefaultCulture = canonicalDefault;

        await dbContext.SaveChangesAsync(cancellationToken);
        _resolvedByCulture.Clear();
    }

    /// <inheritdoc />
    public async Task DeleteLanguagesAsync(CancellationToken cancellationToken = default)
    {
        var entities = await dbContext.LanguageSettings.ToListAsync(cancellationToken);

        if (entities.Count == 0)
        {
            return;
        }

        dbContext.LanguageSettings.RemoveRange(entities);
        await dbContext.SaveChangesAsync(cancellationToken);
        _resolvedByCulture.Clear();
    }

    /// <summary>
    /// Resolves which cultures are offered and which is the default: the stored row, resolved against
    /// what is currently shipped, else every shipped culture with no default.
    /// </summary>
    private async Task<LanguageState> ResolveLanguageStateAsync(CancellationToken cancellationToken)
    {
        // Only the two columns resolution reads are selected, into an untracked LanguageSetting.
        var own = await dbContext.LanguageSettings
            .AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => new LanguageSetting { EnabledCultures = x.EnabledCultures, DefaultCulture = x.DefaultCulture })
            .FirstOrDefaultAsync(cancellationToken);

        var shippedCultures = resourceStore.ShippedCultures.ToList();
        var ownResolved = ResolveRow(own, shippedCultures);

        return new LanguageState(
            EnabledCultures: ownResolved?.EnabledCultures ?? shippedCultures,
            // The stored row is taken as a unit: once there is one, its DefaultCulture is the answer even
            // when that is null (an administrator may deliberately configure no default).
            DefaultCulture: ownResolved?.DefaultCulture,
            HasOwnRow: ownResolved is not null);
    }

    /// <summary>
    /// Filters a stored row's enabled cultures down to what is still shipped, and clears its default
    /// when that default is not itself among the shipped, still-enabled cultures. Returns
    /// <see langword="null"/> for a missing row, or for one whose enabled set is now empty - both read
    /// as "no row" to the caller.
    /// </summary>
    private static RowResolution? ResolveRow(LanguageSetting? row, List<string> shippedCultures)
    {
        if (row is null)
        {
            return null;
        }

        // Mapped to the shipped spelling rather than filtered as stored, so a row can never hand the web a
        // culture code in a casing the router does not know.
        string? Shipped(string culture) => shippedCultures.FirstOrDefault(c => string.Equals(c, culture, StringComparison.OrdinalIgnoreCase));

        var enabled = row.EnabledCultures.Select(Shipped).OfType<string>().Distinct().ToList();
        if (enabled.Count == 0)
        {
            return null;
        }

        var defaultCulture = row.DefaultCulture is not null && Shipped(row.DefaultCulture) is { } shippedDefault && enabled.Contains(shippedDefault)
            ? shippedDefault
            : null;

        return new RowResolution(enabled, defaultCulture);
    }

    /// <summary>A stored row's enabled cultures and default, already filtered to what is still shipped.</summary>
    [NoDirectUse]
    private sealed record RowResolution(List<string> EnabledCultures, string? DefaultCulture);

    /// <summary>Maps an incoming culture to its canonical shipped form, or returns it unchanged when it is not shipped at all.</summary>
    private string Canonicalize(string culture) => resourceStore.TryCanonicalize(culture) ?? culture;

    /// <summary>
    /// Picks which enabled culture actually gets served: <paramref name="requestedCulture"/> if it is
    /// enabled, else the default if that is enabled, else English if enabled, else the first enabled
    /// culture - <paramref name="enabledCultures"/> is never empty.
    /// </summary>
    private static string ResolveServedCulture(string requestedCulture, List<string> enabledCultures, string? defaultCulture)
        => FindCaseInsensitive(enabledCultures, requestedCulture)
           ?? (defaultCulture is not null ? FindCaseInsensitive(enabledCultures, defaultCulture) : null)
           ?? FindCaseInsensitive(enabledCultures, "en")
           ?? enabledCultures[0];

    private static string? FindCaseInsensitive(List<string> cultures, string code)
        => cultures.FirstOrDefault(c => string.Equals(c, code, StringComparison.OrdinalIgnoreCase));

    private static LanguageDto ToLanguageDto(string code)
        => LanguageCatalog.TryGet(code) is { } info
            ? new LanguageDto { Code = code, Name = info.Name, IsRtl = info.IsRtl }
            : new LanguageDto { Code = code, Name = code, IsRtl = false };

    /// <summary>
    /// The resolved language configuration and whether a row is stored at all. Carries
    /// <see cref="NoDirectUseAttribute"/> because it is nested inside <see cref="LocalizationService"/>,
    /// itself marked: without the attribute the architecture test would scan this type and read the
    /// nesting itself as a direct use.
    /// </summary>
    [NoDirectUse]
    private sealed record LanguageState(
        List<string> EnabledCultures,
        string? DefaultCulture,
        bool HasOwnRow);
}

/// <summary>One culture offered to a caller: its code, English display name, and writing direction.</summary>
public sealed class LanguageDto
{
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public bool IsRtl { get; set; }
}

/// <summary>The resources resolved for one request: the culture actually served, and its resources.</summary>
public sealed class ResolvedResources
{
    public string Culture { get; set; } = null!;
    public string? DefaultCulture { get; set; }
    public List<LanguageDto> Languages { get; set; } = [];
    public Dictionary<string, string> Resources { get; set; } = [];
}

/// <summary>
/// The effective language settings, together with what applies with no stored row - every shipped
/// culture, no default.
/// </summary>
public sealed class LanguageResolution
{
    public List<LanguageDto> Languages { get; set; } = [];
    public List<string> EnabledCultures { get; set; } = [];
    public string? DefaultCulture { get; set; }
    public bool IsInherited { get; set; }
    public List<string> InheritedEnabledCultures { get; set; } = [];
    public string? InheritedDefaultCulture { get; set; }
}

/// <summary>One row of the text-override editor: a key, its shipped value, what it falls back to, and its override if it has one.</summary>
public sealed class LocalizationTextRow
{
    public string Key { get; set; } = null!;
    public string DefaultValue { get; set; } = null!;
    public string InheritedValue { get; set; } = null!;
    public string? Value { get; set; }
}

/// <summary>One page of the text-override editor.</summary>
public sealed class LocalizationTextPage
{
    public List<LocalizationTextRow> Items { get; set; } = [];
    public int Total { get; set; }
}
