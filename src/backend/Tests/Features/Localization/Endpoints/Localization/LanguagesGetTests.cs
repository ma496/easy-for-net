namespace Backend.Tests.Features.Localization.Endpoints.Localization;

using Backend.Features.Localization.Core.Entities;
using Backend.Features.Localization.Endpoints.Localization;

/// <summary>
/// Tests for <see cref="LanguagesGetEndpoint"/>: row-level inheritance between a tenant's own settings,
/// the platform's, and the shipped defaults, and the permission it requires.
/// </summary>
public class LanguagesGetTests(App app) : LocalizationTestsBase(app)
{
    [Fact]
    public async Task With_No_Row_Anywhere_Every_Shipped_Culture_Is_Enabled_With_No_Default()
    {
        var tenant = await CreateTenantAsync();
        var roleId = await CreateTenantRoleAsync(tenant.Id, Allow.Localization_View);
        var member = await CreateTenantUserAsync(tenant.Id, roleId);
        var client = await ClientForAsync(member.Username, tenant.Id);
        var shippedCultures = ResourceStore.ShippedCultures;

        var (response, result) = await client.GETAsync<LanguagesGetEndpoint, LanguagesGetResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.IsInherited.Should().BeTrue();
        result.EnabledCultures.Should().BeEquivalentTo(shippedCultures);
        result.DefaultCulture.Should().BeNull();
        result.InheritedEnabledCultures.Should().BeEquivalentTo(shippedCultures);
        result.InheritedDefaultCulture.Should().BeNull();
        result.Languages.Select(l => l.Code).Should().BeEquivalentTo(shippedCultures,
            "this response lists every shipped culture, not only the enabled ones");
    }

    [Fact]
    public async Task Tenant_With_No_Own_Row_Inherits_The_Platforms()
    {
        var second = RequireSecondCulture();
        var tenant = await CreateTenantAsync();
        var roleId = await CreateTenantRoleAsync(tenant.Id, Allow.Localization_View);
        var member = await CreateTenantUserAsync(tenant.Id, roleId);
        var client = await ClientForAsync(member.Username, tenant.Id);

        await SetPlatformLanguagesAsync(["en", second], second);

        var (_, result) = await client.GETAsync<LanguagesGetEndpoint, LanguagesGetResponse>();

        result.IsInherited.Should().BeTrue();
        result.EnabledCultures.Should().BeEquivalentTo(["en", second]);
        result.DefaultCulture.Should().Be(second);
        result.InheritedEnabledCultures.Should().BeEquivalentTo(["en", second]);
        result.InheritedDefaultCulture.Should().Be(second);
    }

    [Fact]
    public async Task Tenants_Own_Row_Outranks_The_Platforms_But_Inherited_Still_Reports_It()
    {
        var (platformCulture, tenantCulture) = RequireTwoOtherCultures();
        var tenant = await CreateTenantAsync();
        var roleId = await CreateTenantRoleAsync(tenant.Id, Allow.Localization_Update, Allow.Localization_View);
        var member = await CreateTenantUserAsync(tenant.Id, roleId);
        var client = await ClientForAsync(member.Username, tenant.Id);

        await SetPlatformLanguagesAsync(["en", platformCulture], platformCulture);
        var (putResponse, _) = await client.PUTAsync<LanguagesUpdateEndpoint, LanguagesUpdateRequest, EmptyResponse>(
            new() { EnabledCultures = ["en", tenantCulture], DefaultCulture = tenantCulture });
        putResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var (_, result) = await client.GETAsync<LanguagesGetEndpoint, LanguagesGetResponse>();

        result.IsInherited.Should().BeFalse();
        result.EnabledCultures.Should().BeEquivalentTo(["en", tenantCulture]);
        result.DefaultCulture.Should().Be(tenantCulture);
        result.InheritedEnabledCultures.Should().BeEquivalentTo(["en", platformCulture],
            "what the tenant would inherit without its own row is still the platform's, even though its own row now wins");
        result.InheritedDefaultCulture.Should().Be(platformCulture);
    }

    [Fact]
    public async Task Tenants_Own_Row_With_No_Default_Reports_Null_Even_When_The_Platform_Has_One()
    {
        var (platformCulture, tenantCulture) = RequireTwoOtherCultures();
        var tenant = await CreateTenantAsync();
        var roleId = await CreateTenantRoleAsync(tenant.Id, Allow.Localization_Update, Allow.Localization_View);
        var member = await CreateTenantUserAsync(tenant.Id, roleId);
        var client = await ClientForAsync(member.Username, tenant.Id);

        await SetPlatformLanguagesAsync(["en", platformCulture], platformCulture);
        var (putResponse, _) = await client.PUTAsync<LanguagesUpdateEndpoint, LanguagesUpdateRequest, EmptyResponse>(
            new() { EnabledCultures = ["en", tenantCulture], DefaultCulture = null });
        putResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var (_, languages) = await client.GETAsync<LanguagesGetEndpoint, LanguagesGetResponse>();

        languages.IsInherited.Should().BeFalse();
        languages.DefaultCulture.Should().BeNull(
            "the tenant's own row is taken as a unit, so it deliberately configuring no default must not fall back to the platform's");
        languages.InheritedDefaultCulture.Should().Be(platformCulture);

        var (_, resources) = await client
            .GETAsync<ResourcesGetEndpoint, ResourcesGetRequest, ResourcesGetResponse>(new() { Culture = "en" });
        resources.DefaultCulture.Should().BeNull("the resources endpoint reports the same per-row default as the languages endpoint");
    }

    [Fact]
    public async Task Own_Row_Drops_A_Stored_Culture_That_Is_No_Longer_Shipped()
    {
        var second = RequireSecondCulture();
        var tenant = await CreateTenantAsync();
        var roleId = await CreateTenantRoleAsync(tenant.Id, Allow.Localization_View);
        var member = await CreateTenantUserAsync(tenant.Id, roleId);
        var client = await ClientForAsync(member.Username, tenant.Id);

        await SetTenantLanguagesAsync(tenant.Id, ["en", second, UnshippedCulture], UnshippedCulture);

        var (_, result) = await client.GETAsync<LanguagesGetEndpoint, LanguagesGetResponse>();

        result.IsInherited.Should().BeFalse();
        result.EnabledCultures.Should().BeEquivalentTo(["en", second],
            "a culture the resource files no longer ship is dropped from a stored row");
        result.DefaultCulture.Should().BeNull("the stored default is itself unshipped, so it does not survive the intersection");
    }

    [Fact]
    public async Task Own_Row_With_Nothing_Left_Shipped_Falls_Back_To_Inherited()
    {
        var tenant = await CreateTenantAsync();
        var roleId = await CreateTenantRoleAsync(tenant.Id, Allow.Localization_View);
        var member = await CreateTenantUserAsync(tenant.Id, roleId);
        var client = await ClientForAsync(member.Username, tenant.Id);
        var shippedCultures = ResourceStore.ShippedCultures;

        await SetTenantLanguagesAsync(tenant.Id, [UnshippedCulture]);

        var (_, result) = await client.GETAsync<LanguagesGetEndpoint, LanguagesGetResponse>();

        result.IsInherited.Should().BeTrue("a row with nothing shipped left counts as no row at all");
        result.EnabledCultures.Should().BeEquivalentTo(shippedCultures);
    }

    [Fact]
    public async Task Platform_Scope_Has_Nothing_Beneath_It_But_Shipped()
    {
        var second = RequireSecondCulture();
        await SetPlatformAdminAuthTokenAsync();
        var shippedCultures = ResourceStore.ShippedCultures;

        var (putResponse, _) = await Client.PUTAsync<LanguagesUpdateEndpoint, LanguagesUpdateRequest, EmptyResponse>(
            new() { EnabledCultures = ["en", second], DefaultCulture = second });
        putResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var (_, result) = await Client.GETAsync<LanguagesGetEndpoint, LanguagesGetResponse>();

        result.IsInherited.Should().BeFalse();
        result.InheritedEnabledCultures.Should().BeEquivalentTo(shippedCultures,
            "platform scope's own row is the platform's, so there is no further layer beneath it but the shipped defaults");
        result.InheritedDefaultCulture.Should().BeNull();
    }

    [Fact]
    public async Task Missing_Permission_Is_Forbidden()
    {
        var tenant = await CreateTenantAsync();
        var member = await CreateTenantUserAsync(tenant.Id);
        var client = await ClientForAsync(member.Username, tenant.Id);

        var (response, _) = await client.GETAsync<LanguagesGetEndpoint, ProblemDetails>();

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Unauthenticated_Caller_Is_Refused()
    {
        var (response, _) = await Client.GETAsync<LanguagesGetEndpoint, ProblemDetails>();

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
