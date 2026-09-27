namespace Backend.Tests.Features.Localization;

using Backend.Features.Localization.Core;
using Backend.Features.Localization.Core.Entities;
using Backend.Tests.Features.Tenancy;

/// <summary>
/// Base class for the localization suite. A tenant's own overrides are already isolated by
/// <see cref="TenancyTestsBase.CreateTenantAsync"/> giving every test a fresh tenant, but a platform
/// override or a platform <see cref="LanguageSetting"/> row is process-wide state with no tenant of its
/// own to isolate it - and every acting scope, tenant sessions included, reads through it. Every test
/// that writes one therefore shares this class's collection, which keeps them from racing each other,
/// and every platform-scope row is wiped after the test that wrote it so the next test - and a
/// genuinely anonymous read, which sees the platform layer with nothing narrower to prefer - starts
/// from the shipped defaults.
/// </summary>
[Collection("Localization")]
public abstract class LocalizationTestsBase(App app) : TenancyTestsBase(app)
{
    /// <summary>A culture guaranteed to be shipped, used wherever the specific culture does not matter.</summary>
    protected const string Culture = "en";

    /// <summary>
    /// A culture code guaranteed never to be shipped, short enough to fit the columns it is written
    /// into directly - used to plant a stored row the shipped/enabled intersection must drop.
    /// </summary>
    protected const string UnshippedCulture = "xx";

    /// <summary>The resource store, for reading a real shipped key rather than a hard-coded one.</summary>
    protected ILocalizationResourceStore ResourceStore => Service<ILocalizationResourceStore>();

    /// <summary>
    /// A shipped English key, read straight off the store so a resource file edit cannot make this
    /// suite fail for a reason unrelated to what it tests.
    /// </summary>
    protected string AnyShippedKey() => ResourceStore.EnglishResources.Keys.First();

    /// <summary>
    /// Writes a platform override directly, standing in for what an administrator acting in no tenant
    /// would have saved through <c>PUT /localization/texts</c>.
    /// </summary>
    protected async Task SetPlatformTextAsync(string culture, string key, string value)
    {
        using var platformScope = TenantContext.BeginPlatformScope();
        DbContext.LocalizationTexts.Add(new LocalizationText { Culture = culture, Key = key, Value = value });
        await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Writes a tenant's override directly, standing in for what its administrator would have saved
    /// through <c>PUT /localization/texts</c>.
    /// </summary>
    protected async Task SetTenantTextAsync(Guid tenantId, string culture, string key, string value)
    {
        using var tenantScope = TenantContext.BeginTenant(tenantId);
        DbContext.LocalizationTexts.Add(new LocalizationText { Culture = culture, Key = key, Value = value });
        await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Writes the platform's language settings row directly.</summary>
    protected async Task SetPlatformLanguagesAsync(IEnumerable<string> enabledCultures, string? defaultCulture = null)
    {
        using var platformScope = TenantContext.BeginPlatformScope();
        DbContext.LanguageSettings.Add(new LanguageSetting { EnabledCultures = [.. enabledCultures], DefaultCulture = defaultCulture });
        await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Writes a tenant's language settings row directly, bypassing the endpoint's validation - used to
    /// plant a row carrying a culture the shipped resource files no longer declare.
    /// </summary>
    protected async Task SetTenantLanguagesAsync(Guid tenantId, IEnumerable<string> enabledCultures, string? defaultCulture = null)
    {
        using var tenantScope = TenantContext.BeginTenant(tenantId);
        DbContext.LanguageSettings.Add(new LanguageSetting { EnabledCultures = [.. enabledCultures], DefaultCulture = defaultCulture });
        await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// One shipped culture other than English, for a test that needs a second culture to exercise
    /// override or inheritance behaviour. Skips the test - rather than asserting on a language that does
    /// not exist - when the project ships English only, as a project generated with <c>-m false</c> does.
    /// </summary>
    protected string RequireSecondCulture()
    {
        var culture = ResourceStore.ShippedCultures.FirstOrDefault(c => !string.Equals(c, Culture, StringComparison.OrdinalIgnoreCase));
        Assert.SkipWhen(culture is null, "this test needs a second shipped culture, and the project ships English only");
        return culture!;
    }

    /// <summary>
    /// Two distinct shipped cultures other than English, for a test that must tell a tenant's own
    /// setting apart from what it inherits. Skips when fewer than two non-English cultures are shipped.
    /// </summary>
    protected (string First, string Second) RequireTwoOtherCultures()
    {
        var others = ResourceStore.ShippedCultures.Where(c => !string.Equals(c, Culture, StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.SkipWhen(others.Count < 2, "this test needs two shipped cultures besides English, and the project does not ship that many");
        return (others[0], others[1]);
    }

    /// <summary>
    /// Removes every platform-scope localization row this test may have written - directly or through
    /// an endpoint - so the next test in this collection starts from the shipped defaults exactly as
    /// the one before it did.
    /// </summary>
    protected override async ValueTask TearDownAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var texts = await DbContext.LocalizationTexts.AcrossAllTenants()
            .Where(x => x.TenantId == null)
            .ToListAsync(cancellationToken);
        var languages = await DbContext.LanguageSettings.AcrossAllTenants()
            .Where(x => x.TenantId == null)
            .ToListAsync(cancellationToken);

        if (texts.Count > 0 || languages.Count > 0)
        {
            DbContext.LocalizationTexts.RemoveRange(texts);
            DbContext.LanguageSettings.RemoveRange(languages);
            await DbContext.SaveChangesAsync(cancellationToken);
        }

        await base.TearDownAsync();
    }
}
