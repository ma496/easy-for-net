namespace Backend.Tests.Features.Localization.Endpoints.Localization;

using Backend.Features.Localization.Endpoints.Localization;

/// <summary>
/// Tests for <see cref="ResourcesGetEndpoint"/>: the resolution chain (tenant beats platform beats
/// shipped), tenant isolation, what an anonymous caller sees, and the served-culture fallback.
/// </summary>
public class ResourcesGetTests(App app) : LocalizationTestsBase(app)
{
    [Fact]
    public async Task Tenant_Override_Beats_Platform_Beats_Shipped()
    {
        var tenant = await CreateTenantAsync();
        var member = await CreateTenantUserAsync(tenant.Id);
        var key = AnyShippedKey();

        await SetPlatformTextAsync(Culture, key, "platform override");
        await SetTenantTextAsync(tenant.Id, Culture, key, "tenant override");

        var client = await ClientForAsync(member.Username, tenant.Id);
        var (response, resources) = await client
            .GETAsync<ResourcesGetEndpoint, ResourcesGetRequest, ResourcesGetResponse>(new() { Culture = Culture });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        resources.Resources[key].Should().Be("tenant override",
            "the acting tenant's own override outranks the platform's and the shipped value beneath it");
    }

    [Fact]
    public async Task Platform_Override_Applies_With_No_Tenant_Override()
    {
        var tenant = await CreateTenantAsync();
        var member = await CreateTenantUserAsync(tenant.Id);
        var key = AnyShippedKey();

        await SetPlatformTextAsync(Culture, key, "platform override");

        var client = await ClientForAsync(member.Username, tenant.Id);
        var (_, resources) = await client
            .GETAsync<ResourcesGetEndpoint, ResourcesGetRequest, ResourcesGetResponse>(new() { Culture = Culture });

        resources.Resources[key].Should().Be("platform override",
            "with no tenant override the platform's is what applies, ahead of the shipped value");
    }

    [Fact]
    public async Task Second_Tenants_Session_Sees_None_Of_The_Firsts_Override()
    {
        var tenantA = await CreateTenantAsync();
        var tenantB = await CreateTenantAsync();
        var memberB = await CreateTenantUserAsync(tenantB.Id);
        var key = AnyShippedKey();
        var shipped = ResourceStore.EnglishResources[key];

        await SetTenantTextAsync(tenantA.Id, Culture, key, "tenant A only");

        var clientB = await ClientForAsync(memberB.Username, tenantB.Id);
        var (_, resources) = await clientB
            .GETAsync<ResourcesGetEndpoint, ResourcesGetRequest, ResourcesGetResponse>(new() { Culture = Culture });

        resources.Resources[key].Should().Be(shipped,
            "a tenant's own override never crosses into another tenant's acting scope");
    }

    [Fact]
    public async Task Anonymous_Caller_Sees_Platform_Overrides_Only()
    {
        var tenant = await CreateTenantAsync();
        var key = AnyShippedKey();

        await SetPlatformTextAsync(Culture, key, "platform value");
        await SetTenantTextAsync(tenant.Id, Culture, key, "must not be visible anonymously");

        var (response, resources) = await Client
            .GETAsync<ResourcesGetEndpoint, ResourcesGetRequest, ResourcesGetResponse>(new() { Culture = Culture });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        resources.Resources[key].Should().Be("platform value",
            "an anonymous caller has no tenant scope, so only the platform layer can reach it");
    }

    [Fact]
    public async Task Disabled_Requested_Culture_Falls_Back_To_The_Configured_Default()
    {
        var second = RequireSecondCulture();
        await SetPlatformLanguagesAsync(["en", second], defaultCulture: second);

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
        await SetPlatformLanguagesAsync([first, second]);

        var (_, resources) = await Client
            .GETAsync<ResourcesGetEndpoint, ResourcesGetRequest, ResourcesGetResponse>(new() { Culture = "not-enabled" });

        resources.Culture.Should().Be(first,
            "neither the request nor a default names an enabled culture, and English itself is not enabled, so the first enabled culture is served");
    }

    [Fact]
    public async Task Languages_List_Only_The_Enabled_Cultures()
    {
        var second = RequireSecondCulture();
        await SetPlatformLanguagesAsync(["en", second]);

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
