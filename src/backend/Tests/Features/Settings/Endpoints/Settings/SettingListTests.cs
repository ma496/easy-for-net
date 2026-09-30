namespace Backend.Tests.Features.Settings.Endpoints.Settings;

using System.Text.Json.Nodes;
using Backend.Features.Settings.Endpoints.Settings;

/// <summary>
/// Tests for <see cref="SettingListEndpoint"/>: every declared setting as the acting scope resolves it,
/// each property's value and the layer it came from, and the permission the listing requires.
/// </summary>
public class SettingListTests(App app) : SettingsTestsBase(app)
{
    [Fact]
    public async Task No_Overrides_Lists_Code_Defaults_With_Source_Default()
    {
        var tenant = await CreateTenantAsync();
        var client = await TenantClientAsync(tenant.Id);

        var (response, list) = await client.GETAsync<SettingListEndpoint, SettingListResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var verification = PropertyOf(list, "Signin", "isEmailVerificationRequired");
        verification.Value!.GetValue<bool>().Should().BeFalse();
        verification.Source.Should().Be("default");

        PropertyOf(list, Probe, "limit").Value!.GetValue<int>().Should().Be(10);
        PropertyOf(list, Probe, "label").Value!.GetValue<string>().Should().Be("default");
        PropertyOf(list, Probe, "enabled").Value!.GetValue<bool>().Should().BeFalse();
        list.Items.Single(x => x.Name == Probe).Properties.Should().OnlyContain(x => x.Source == "default");
    }

    [Fact]
    public async Task Platform_Scope_Lists_The_Platforms_Overrides_And_No_Tenant_Layer()
    {
        var tenant = await CreateTenantAsync();
        await SetTenantValuesAsync(tenant.Id, Probe, new JsonObject { ["label"] = "tenant-only" });
        await SetPlatformValuesAsync(Probe, new JsonObject { ["limit"] = 55 });
        var client = await PlatformClientAsync();

        var (response, list) = await client.GETAsync<SettingListEndpoint, SettingListResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        PropertyOf(list, Probe, "limit").Value!.GetValue<int>().Should().Be(55);
        PropertyOf(list, Probe, "limit").Source.Should().Be("platform");
        PropertyOf(list, Probe, "label").Value!.GetValue<string>().Should().Be("default", "a tenant's override is not the platform's");
        PropertyOf(list, Probe, "label").Source.Should().Be("default");
    }

    [Fact]
    public async Task Tenant_Scope_Lists_Its_Own_Overrides_Over_The_Platforms()
    {
        var tenant = await CreateTenantAsync();
        await SetPlatformValuesAsync(Probe, new JsonObject { ["limit"] = 55, ["label"] = "platform" });
        await SetTenantValuesAsync(tenant.Id, Probe, new JsonObject { ["label"] = "mine" });
        var client = await TenantClientAsync(tenant.Id);

        var (response, list) = await client.GETAsync<SettingListEndpoint, SettingListResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        PropertyOf(list, Probe, "label").Value!.GetValue<string>().Should().Be("mine");
        PropertyOf(list, Probe, "label").Source.Should().Be("tenant");
        PropertyOf(list, Probe, "limit").Value!.GetValue<int>().Should().Be(55);
        PropertyOf(list, Probe, "limit").Source.Should().Be("platform");
        PropertyOf(list, Probe, "enabled").Source.Should().Be("default");
    }

    [Fact]
    public async Task Caller_Without_Settings_View_Is_Forbidden()
    {
        var tenant = await CreateTenantAsync();
        var client = await TenantClientAsync(tenant.Id, Allow.Localization_View);

        var (response, _) = await client.GETAsync<SettingListEndpoint, ProblemDetails>();

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Anonymous_Caller_Is_Unauthorized()
    {
        ClearAuthToken();

        var (response, _) = await Client.GETAsync<SettingListEndpoint, ProblemDetails>();

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
