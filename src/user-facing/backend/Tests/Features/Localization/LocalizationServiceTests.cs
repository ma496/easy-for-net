using Backend.Features.Localization.Core;

namespace Backend.Tests.Features.Localization;

/// <summary>
/// Tests for <see cref="LocalizationService"/>: the culture it serves, the override layered over the
/// shipped text, and its per-instance resolution cache, which a write through the same instance drops.
/// </summary>
public class LocalizationServiceTests(App app) : LocalizationTestsBase(app)
{
    private ILocalizationService LocalizationService => Service<ILocalizationService>();

    [Fact]
    public async Task An_Unknown_Culture_Is_Served_As_English()
    {
        var resolved = await LocalizationService.ResolveResourcesAsync("zz", TestContext.Current.CancellationToken);

        resolved.Culture.Should().Be("en");
        resolved.Resources.Should().ContainKey(AnyShippedKey());
    }

    [Fact]
    public async Task A_Write_Through_The_Same_Instance_Is_Seen_By_The_Next_Resolution()
    {
        var key = AnyShippedKey();
        var cancellationToken = TestContext.Current.CancellationToken;

        var before = await LocalizationService.ResolveResourcesAsync(Culture, cancellationToken);
        before.Resources[key].Should().Be(ResourceStore.EnglishResources[key]);

        await LocalizationService.SetTextAsync(Culture, key, "overridden", cancellationToken);
        (await LocalizationService.ResolveResourcesAsync(Culture, cancellationToken)).Resources[key].Should().Be("overridden");

        await LocalizationService.DeleteTextAsync(Culture, key, cancellationToken);
        (await LocalizationService.ResolveResourcesAsync(Culture, cancellationToken)).Resources[key]
            .Should().Be(ResourceStore.EnglishResources[key], "removing the override restores the shipped text");
    }
}
