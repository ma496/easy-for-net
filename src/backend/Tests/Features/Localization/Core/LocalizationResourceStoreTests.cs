namespace Backend.Tests.Features.Localization.Core;

using Backend.Features.Localization.Core;

/// <summary>
/// Shipped-resource consistency: every locale mirrors English's key set, no shipped value is empty,
/// and the language catalog carries metadata for every shipped culture. The web app's
/// <c>i18n/locales.test.ts</c> checks the other half: that its routing locales are exactly these files.
/// </summary>
/// <remarks>
/// No collection is needed: the resource store is a read-only singleton over the embedded files, and
/// nothing in this class writes a row any other test could see.
/// </remarks>
public class LocalizationResourceStoreTests(App app) : AppTestsBase(app)
{
    private ILocalizationResourceStore ResourceStore => Service<ILocalizationResourceStore>();

    [Fact]
    public void English_Is_Shipped()
    {
        ResourceStore.ShippedCultures.Should().Contain("en",
            "English is the fallback every other culture is measured against, and the base every resolved response starts from");
    }

    [Fact]
    public void Every_Shipped_Culture_Has_A_Catalog_Entry()
    {
        ResourceStore.ShippedCultures.Should().AllSatisfy(culture =>
            LanguageCatalog.Contains(culture).Should().BeTrue(
                $"'{culture}' is shipped but the catalog carries no display metadata for it"));
    }

    [Fact]
    public void Every_Locale_Declares_Exactly_Englishs_Key_Set()
    {
        var englishKeys = ResourceStore.EnglishResources.Keys.ToHashSet(StringComparer.Ordinal);

        foreach (var culture in ResourceStore.ShippedCultures.Where(c => !string.Equals(c, "en", StringComparison.OrdinalIgnoreCase)))
        {
            var keys = ResourceStore.GetResources(culture)!.Keys.ToHashSet(StringComparer.Ordinal);

            keys.Should().BeEquivalentTo(englishKeys,
                $"'{culture}' must declare exactly the keys English does - creating a key the shipped English file does not declare is out of scope");
        }
    }

    [Fact]
    public void No_Shipped_Value_Is_Empty()
    {
        foreach (var culture in ResourceStore.ShippedCultures)
        {
            ResourceStore.GetResources(culture)!.Should().AllSatisfy(pair =>
                pair.Value.Should().NotBeNullOrWhiteSpace(
                    $"'{pair.Key}' in '{culture}' would otherwise show as literally empty text in the UI"));
        }
    }

    [Fact]
    public void No_Shipped_Value_Equals_Its_Own_Key()
    {
        foreach (var culture in ResourceStore.ShippedCultures)
        {
            ResourceStore.GetResources(culture)!.Should().AllSatisfy(pair =>
                pair.Value.Should().NotBe(pair.Key,
                    $"'{pair.Key}' in '{culture}' reads as an untranslated placeholder rather than real copy"));
        }
    }
}
