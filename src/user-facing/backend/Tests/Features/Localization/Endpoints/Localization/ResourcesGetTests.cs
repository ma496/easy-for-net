using Backend.Features.Localization.Endpoints.Localization;

namespace Backend.Tests.Features.Localization.Endpoints.Localization;

/// <summary>
/// Tests for <see cref="ResourcesGetEndpoint"/>: the resolution chain (tenant beats platform beats
/// shipped), tenant isolation, what an anonymous caller sees, and the served-culture fallback.
/// </summary>
public class ResourcesGetTests(App app) : LocalizationTestsBase(app)
{
    [Fact]
    public async Task Disabled_Requested_Culture_Falls_Back_To_The_Configured_Default()
    {
        var second = RequireSecondCulture();
        await SetLanguagesAsync(["en", second], defaultCulture: second);

        var (response, resources) = await Client
            .GETAsync<ResourcesGetEndpoint, ResourcesGetRequest, ResourcesGetResponse>(new() { Culture = "not-enabled" });

        response.StatusCode.Should().Be(HttpStatusCode.OK, "an unenabled culture still falls back rather than failing the request");
        resources.Culture.Should().Be(second, "the requested culture is not enabled, so the scope's configured default is served instead");
    }

    [Fact]
    public async Task Unknown_Culture_Falls_Back_To_English_And_Still_Answers_200()
    {
        var (response, resources) = await Client
            .GETAsync<ResourcesGetEndpoint, ResourcesGetRequest, ResourcesGetResponse>(new() { Culture = "not-a-real-culture" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        resources.Culture.Should().Be("en", "with no configured default, English is served whenever it is enabled");
    }

    [Fact]
    public async Task With_No_Default_And_English_Disabled_The_First_Enabled_Culture_Is_Served()
    {
        var (first, second) = RequireTwoOtherCultures();
        await SetLanguagesAsync([first, second]);

        var (_, resources) = await Client
            .GETAsync<ResourcesGetEndpoint, ResourcesGetRequest, ResourcesGetResponse>(new() { Culture = "not-enabled" });

        resources.Culture.Should().Be(first,
            "neither the request nor a default names an enabled culture, and English itself is not enabled, so the first enabled culture is served");
    }

    [Fact]
    public async Task Languages_List_Only_The_Enabled_Cultures()
    {
        var second = RequireSecondCulture();
        await SetLanguagesAsync(["en", second]);

        var (_, resources) = await Client
            .GETAsync<ResourcesGetEndpoint, ResourcesGetRequest, ResourcesGetResponse>(new() { Culture = "en" });

        resources.Languages.Select(l => l.Code).Should().BeEquivalentTo(["en", second]);
    }

    [Fact]
    public async Task Uppercase_Route_Culture_Serves_The_Canonical_Culture()
    {
        var (response, resources) = await Client
            .GETAsync<ResourcesGetEndpoint, ResourcesGetRequest, ResourcesGetResponse>(new() { Culture = "EN" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        resources.Culture.Should().Be("en", "an uppercase route culture still resolves to its canonical shipped code");
    }
}
