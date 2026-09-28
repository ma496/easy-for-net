namespace Backend.Features.Localization.Core;

using Backend.Features.Localization.Core.Entities;
using Backend.Features.Tenancy.Core;

/// <summary>
/// Resolves the translation resources and language settings the caller's acting scope sees, and
/// administers the overrides an administrator edits from the admin screen. Internal to the
/// <c>Localization</c> feature: the global error plumbing that needs to look up a business error's
/// message reaches this through the narrow, published <see cref="IErrorMessageLocalizer"/> instead of
/// depending on this interface directly - see that type for the rationale.
/// </summary>
/// <remarks>
/// A text key resolves first answer wins: the acting tenant's own override, then the platform's
/// (<see cref="LocalizationText.TenantId"/> <see langword="null"/>), then the shipped value for the
/// requested culture, then the shipped English value. An anonymous caller, and a session acting in no
/// tenant, sees platform overrides only - there is nothing beneath the platform to inherit from.
/// <para>
/// Which cultures a scope offers, and which of them is the default, resolve per row rather than per
/// key: the acting scope's own <see cref="LanguageSetting"/> row if it has one, else the platform's,
/// else every shipped culture enabled with no configured default.
/// </para>
/// </remarks>
internal interface ILocalizationService
{
    /// <summary>
    /// Resolves the resources served for a request naming <paramref name="requestedCulture"/>: the
    /// culture actually served (the requested one if enabled, else the scope's default, else English if
    /// enabled, else the first enabled culture), the enabled languages, and the merged resource
    /// dictionary for the served culture.
    /// </summary>
    Task<ResolvedResources> ResolveResourcesAsync(string requestedCulture, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the localized message for a business error carrying <paramref name="errorCode"/>, or
    /// <see langword="null"/> when the served culture (the same resolution <see cref="ResolveResourcesAsync"/>
    /// documents - the acting tenant's override, then the platform's, then the shipped culture, then
    /// shipped English) has no <c>error.server.&lt;code&gt;</c> key at all - which is how a plain
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
    /// <param name="sessionTenantId">
    /// The tenant the caller's session names, read from its <c>tenant_id</c> claim - read only when
    /// this instance's own <see cref="ITenantContext"/> was never given a scope to begin with, which is
    /// the case for the handful of error responses built ahead of <c>TenantContextProcessor</c> (the
    /// permission refusal <c>AuthorizationRefusalResultHandler</c> answers with, in particular). It
    /// decides nothing but which override text applies; an unresolved tenant context still reads and
    /// writes nothing on its account.
    /// </param>
    /// <param name="cancellationToken">Cancels the resource resolution.</param>
    Task<string?> TryLocalizeErrorAsync(string requestedCulture, string errorCode, string propertyName, Guid? sessionTenantId = null, CancellationToken cancellationToken = default);

    /// <summary>Resolves the effective and inherited language settings for the acting scope.</summary>
    Task<LanguageResolution> ResolveLanguagesAsync(CancellationToken cancellationToken = default);

    /// <summary>Builds one page of the text-override editor for the acting scope and culture.</summary>
    Task<LocalizationTextPage> GetTextsAsync(
        string culture, string? filter, bool onlyOverridden, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>Upserts the acting scope's override for one key.</summary>
    Task SetTextAsync(string culture, string key, string value, CancellationToken cancellationToken = default);

    /// <summary>Removes the acting scope's override for one key, if it has one. Idempotent.</summary>
    Task DeleteTextAsync(string culture, string key, CancellationToken cancellationToken = default);

    /// <summary>Upserts the acting scope's language settings row.</summary>
    Task SetLanguagesAsync(IReadOnlyList<string> enabledCultures, string? defaultCulture, CancellationToken cancellationToken = default);

    /// <summary>Removes the acting scope's language settings row, if it has one. Idempotent.</summary>
    Task DeleteLanguagesAsync(CancellationToken cancellationToken = default);
}

/// <summary>EF Core-backed implementation of <see cref="ILocalizationService"/>.</summary>
[NoDirectUse]
internal class LocalizationService(AppDbContext dbContext, ITenantContext tenantContext, ILocalizationResourceStore resourceStore)
    : ILocalizationService
{
    /// <summary>
    /// The tenant reads and writes act for, or <see langword="null"/> for platform scope - and also for
    /// a request whose scope was never established at all, which reads exactly as platform scope does:
    /// no tenant override exists to prefer over the platform's. Only <see cref="ResolveResourcesAsync(string, CancellationToken)"/>
    /// can see the unresolved case, because it is the one endpoint anonymous callers reach.
    /// </summary>
    private Guid? EffectiveTenantId => tenantContext.IsResolved ? tenantContext.CurrentTenantId : null;

    /// <summary>
    /// Memoizes a resolution by the tenant it was resolved for and its canonicalized requested culture,
    /// for the lifetime of this instance - one request or job, since the service is registered scoped.
    /// Keyed by tenant rather than culture alone because the acting scope genuinely can change within
    /// that lifetime: a background job iterating several tenants opens <see cref="ITenantContext.BeginTenant"/>
    /// and disposes it more than once in the same DI scope, and <see cref="TryLocalizeErrorAsync"/>
    /// below resolves under a caller-supplied fallback tenant of its own when no scope was ever opened
    /// at all. A key of culture alone would serve either of those a stale or wrong tenant's cache entry.
    /// </summary>
    private readonly Dictionary<(Guid? TenantId, string Culture), ResolvedResources> _resolvedByScope = new();

    /// <inheritdoc />
    public Task<ResolvedResources> ResolveResourcesAsync(string requestedCulture, CancellationToken cancellationToken = default)
        => ResolveResourcesAsync(requestedCulture, EffectiveTenantId, cancellationToken);

    /// <summary>
    /// Does the work <see cref="ResolveResourcesAsync(string, CancellationToken)"/> publishes, against
    /// an explicit <paramref name="effectiveTenantId"/> rather than reading <see cref="EffectiveTenantId"/>
    /// itself - which lets <see cref="TryLocalizeErrorAsync"/> supply its own fallback tenant for the
    /// requests that reach it with no tenant scope established at all.
    /// </summary>
    private async Task<ResolvedResources> ResolveResourcesAsync(string requestedCulture, Guid? effectiveTenantId, CancellationToken cancellationToken)
    {
        requestedCulture = Canonicalize(requestedCulture);
        var cacheKey = (effectiveTenantId, requestedCulture);
        if (_resolvedByScope.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var state = await ResolveLanguageStateAsync(effectiveTenantId, cancellationToken);
        var servedCulture = ResolveServedCulture(requestedCulture, state.EnabledCultures, state.DefaultCulture);

        var isTenantScope = effectiveTenantId is not null;

        // Every override that could apply to the served culture, for the acting tenant and the
        // platform both, in one round trip - AcrossAllTenants because a genuinely anonymous caller has
        // no resolved scope for the automatic "Tenant" filter to read.
        var overrides = await dbContext.LocalizationTexts
            .AsNoTracking()
            .AcrossAllTenants()
            .Where(x => x.Culture == servedCulture && (x.TenantId == effectiveTenantId || x.TenantId == null))
            .ToListAsync(cancellationToken);

        var resources = new Dictionary<string, string>(resourceStore.EnglishResources, StringComparer.Ordinal);
        if (resourceStore.GetResources(servedCulture) is { } shippedForCulture)
        {
            foreach (var (key, value) in shippedForCulture)
            {
                resources[key] = value;
            }
        }

        // Platform overrides apply first, then the tenant's own on top - the resolution order the type
        // documents. In platform scope (or with no scope at all) there is no tenant layer to add.
        foreach (var text in overrides.Where(x => x.TenantId == null))
        {
            resources[text.Key] = text.Value;
        }

        if (isTenantScope)
        {
            foreach (var text in overrides.Where(x => x.TenantId == effectiveTenantId))
            {
                resources[text.Key] = text.Value;
            }
        }

        var result = new ResolvedResources
        {
            Culture = servedCulture,
            DefaultCulture = state.DefaultCulture,
            Languages = [.. state.EnabledCultures.Select(ToLanguageDto)],
            Resources = resources,
        };
        _resolvedByScope[cacheKey] = result;

        return result;
    }

    /// <inheritdoc />
    public async Task<string?> TryLocalizeErrorAsync(string requestedCulture, string errorCode, string propertyName, Guid? sessionTenantId = null, CancellationToken cancellationToken = default)
    {
        // The session's own tenant is used only when this instance's scope was never established at
        // all - a real scope, tenant or platform, always wins over the caller-supplied fallback.
        var effectiveTenantId = tenantContext.IsResolved ? tenantContext.CurrentTenantId : sessionTenantId;
        var resolved = await ResolveResourcesAsync(requestedCulture, effectiveTenantId, cancellationToken);
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
        var state = await ResolveLanguageStateAsync(EffectiveTenantId, cancellationToken);

        return new LanguageResolution
        {
            Languages = [.. resourceStore.ShippedCultures.OrderBy(code => code, StringComparer.Ordinal).Select(ToLanguageDto)],
            EnabledCultures = state.EnabledCultures,
            DefaultCulture = state.DefaultCulture,
            IsInherited = !state.HasOwnRow,
            InheritedEnabledCultures = state.InheritedEnabledCultures,
            InheritedDefaultCulture = state.InheritedDefaultCulture,
        };
    }

    /// <inheritdoc />
    public async Task<LocalizationTextPage> GetTextsAsync(
        string culture, string? filter, bool onlyOverridden, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        culture = Canonicalize(culture);
        var effectiveTenantId = EffectiveTenantId;
        var isTenantScope = effectiveTenantId is not null;

        var overrides = await dbContext.LocalizationTexts
            .AsNoTracking()
            .AcrossAllTenants()
            .Where(x => x.Culture == culture && (x.TenantId == effectiveTenantId || x.TenantId == null))
            .ToListAsync(cancellationToken);

        var ownOverrides = overrides
            .Where(x => x.TenantId == effectiveTenantId)
            .ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);

        // In platform scope this scope's own row and "the platform" are the same row, so there is
        // nothing beneath the shipped value to inherit from.
        var platformOverrides = isTenantScope
            ? overrides.Where(x => x.TenantId == null).ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal)
            : ownOverrides;

        var englishResources = resourceStore.EnglishResources;
        var shippedForCulture = resourceStore.GetResources(culture) ?? englishResources;

        var rows = new List<LocalizationTextRow>(englishResources.Count);
        foreach (var key in englishResources.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            var defaultValue = shippedForCulture.TryGetValue(key, out var shippedValue) ? shippedValue : englishResources[key];
            var inheritedValue = isTenantScope && platformOverrides.TryGetValue(key, out var platformValue)
                ? platformValue
                : defaultValue;
            var value = ownOverrides.TryGetValue(key, out var ownValue) ? ownValue : null;

            rows.Add(new LocalizationTextRow
            {
                Key = key,
                DefaultValue = defaultValue,
                InheritedValue = inheritedValue,
                Value = value,
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

        // Filtered to the acting scope automatically: a tenant session sees only its own rows here, a
        // platform session only the platform's, so this is already "this scope's own override".
        var entity = await dbContext.LocalizationTexts
            .FirstOrDefaultAsync(x => x.Culture == culture && x.Key == key, cancellationToken);

        if (entity is null)
        {
            // TenantId is left unset on purpose: AppDbContext attributes a new row to the active scope
            // on save, which is the only place a tenant attribution may come from.
            entity = new LocalizationText { Culture = culture, Key = key, Value = value };
            dbContext.LocalizationTexts.Add(entity);
        }
        else
        {
            entity.Value = value;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        _resolvedByScope.Clear();
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
        _resolvedByScope.Clear();
    }

    /// <inheritdoc />
    public async Task SetLanguagesAsync(IReadOnlyList<string> enabledCultures, string? defaultCulture, CancellationToken cancellationToken = default)
    {
        // Canonicalized before storage so "FR" and "fr" land as the same stored culture rather than
        // fragmenting it, exactly as a text override's culture does.
        var canonicalEnabled = enabledCultures.Select(Canonicalize).Distinct(StringComparer.Ordinal).ToList();
        var canonicalDefault = defaultCulture is not null ? Canonicalize(defaultCulture) : null;

        var entity = await dbContext.LanguageSettings.FirstOrDefaultAsync(cancellationToken);

        if (entity is null)
        {
            entity = new LanguageSetting();
            dbContext.LanguageSettings.Add(entity);
        }

        entity.EnabledCultures = canonicalEnabled;
        entity.DefaultCulture = canonicalDefault;

        await dbContext.SaveChangesAsync(cancellationToken);
        _resolvedByScope.Clear();
    }

    /// <inheritdoc />
    public async Task DeleteLanguagesAsync(CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.LanguageSettings.FirstOrDefaultAsync(cancellationToken);

        if (entity is null)
        {
            return;
        }

        dbContext.LanguageSettings.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        _resolvedByScope.Clear();
    }

    /// <summary>
    /// Resolves which cultures the acting scope offers and which is its default, following the
    /// row-level inheritance chain, along with what would apply without this scope's own row.
    /// </summary>
    private async Task<LanguageState> ResolveLanguageStateAsync(Guid? effectiveTenantId, CancellationToken cancellationToken)
    {
        var isTenantScope = effectiveTenantId is not null;

        // AcrossAllTenants because this must work with no scope resolved at all (a genuinely anonymous
        // caller of the resources endpoint), which the automatic "Tenant" filter cannot do.
        var own = await dbContext.LanguageSettings
            .AsNoTracking()
            .AcrossAllTenants()
            .FirstOrDefaultAsync(x => x.TenantId == effectiveTenantId, cancellationToken);

        LanguageSetting? platform = null;
        if (isTenantScope)
        {
            platform = await dbContext.LanguageSettings
                .AsNoTracking()
                .AcrossAllTenants()
                .FirstOrDefaultAsync(x => x.TenantId == null, cancellationToken);
        }

        var shippedCultures = resourceStore.ShippedCultures.ToList();

        // A stored row is resolved as a unit against what is currently shipped - a culture the resource
        // files no longer carry is dropped from its enabled set, and a default outside what remains is
        // cleared - and a row that ends up with nothing enabled counts as no row at all, falling back to
        // the next level exactly as a genuinely missing row would.
        var platformResolved = isTenantScope ? ResolveRow(platform, shippedCultures) : null;

        // In platform scope (or unresolved) this scope's own row already is the platform's, so what it
        // would inherit without itself is the shipped defaults - there is no further layer beneath it.
        var inheritedEnabled = platformResolved?.EnabledCultures ?? shippedCultures;
        var inheritedDefault = platformResolved?.DefaultCulture;

        var ownResolved = ResolveRow(own, shippedCultures);

        return new LanguageState(
            EnabledCultures: ownResolved?.EnabledCultures ?? inheritedEnabled,
            // The scope's own row is taken as a unit: once it has one, its DefaultCulture is the answer
            // even when that is null (a scope may deliberately configure no default), rather than
            // falling through to what it inherits.
            DefaultCulture: ownResolved is not null ? ownResolved.DefaultCulture : inheritedDefault,
            HasOwnRow: ownResolved is not null,
            InheritedEnabledCultures: inheritedEnabled,
            InheritedDefaultCulture: inheritedDefault);
    }

    /// <summary>
    /// Filters a stored row's enabled cultures down to what is still shipped, and clears its default
    /// when that default is not itself among the shipped, still-enabled cultures. Returns
    /// <see langword="null"/> for a missing row, or for one whose enabled set is now empty - both read
    /// as "no row of its own" to the caller.
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
    /// enabled, else the scope's default if that is enabled, else English if enabled, else the first
    /// enabled culture - <paramref name="enabledCultures"/> is never empty.
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
    /// The acting scope's resolved language configuration: what it offers, what it would offer without
    /// its own row, and whether it has one at all. Carries <see cref="NoDirectUseAttribute"/> because it
    /// is nested inside <see cref="LocalizationService"/>, itself marked: without the attribute the
    /// architecture test would scan this type and read the nesting itself as a direct use.
    /// </summary>
    [NoDirectUse]
    private sealed record LanguageState(
        List<string> EnabledCultures,
        string? DefaultCulture,
        bool HasOwnRow,
        List<string> InheritedEnabledCultures,
        string? InheritedDefaultCulture);
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

/// <summary>The acting scope's effective and inherited language settings.</summary>
public sealed class LanguageResolution
{
    public List<LanguageDto> Languages { get; set; } = [];
    public List<string> EnabledCultures { get; set; } = [];
    public string? DefaultCulture { get; set; }
    public bool IsInherited { get; set; }
    public List<string> InheritedEnabledCultures { get; set; } = [];
    public string? InheritedDefaultCulture { get; set; }
}

/// <summary>One row of the text-override editor: a key, its shipped value, what it inherits, and this scope's own override if it has one.</summary>
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
