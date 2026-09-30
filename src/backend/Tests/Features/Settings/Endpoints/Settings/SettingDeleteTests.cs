namespace Backend.Tests.Features.Settings.Endpoints.Settings;

using System.Text.Json.Nodes;
using Backend.Features.Settings.Endpoints.Settings;

/// <summary>
/// Tests for <see cref="SettingDeleteEndpoint"/>: removing the acting scope's own overrides - and only
/// those - so the setting follows the layer below again, and the permission it requires.
/// </summary>
public class SettingDeleteTests(App app) : SettingsTestsBase(app)
{
    [Fact]
    public async Task Tenant_Delete_Falls_Back_To_The_Platform_Value()
    {
        var tenant = await CreateTenantAsync();
        await SetPlatformValuesAsync(Probe, new JsonObject { ["limit"] = 42 });
        await SetTenantValuesAsync(tenant.Id, Probe, new JsonObject { ["limit"] = 7 });
        var client = await TenantClientAsync(tenant.Id);

        var (response, _) = await client.DELETEAsync<SettingDeleteEndpoint, SettingDeleteRequest, EmptyResponse>(new() { Name = Probe });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await StoredValuesAsync(Probe, tenant.Id)).Should().BeNull();
        (await StoredValuesAsync(Probe, null))!.ToJsonString().Should().Be("""{"limit":42}""", "a tenant's delete leaves the platform row alone");

        var (_, list) = await client.GETAsync<SettingListEndpoint, SettingListResponse>();
        PropertyOf(list, Probe, "limit").Value!.GetValue<int>().Should().Be(42);
        PropertyOf(list, Probe, "limit").Source.Should().Be("platform");
    }

    [Fact]
    public async Task Platform_Delete_Falls_Back_To_The_Default_And_Leaves_Tenant_Rows()
    {
        var tenant = await CreateTenantAsync();
        await SetPlatformValuesAsync(Probe, new JsonObject { ["limit"] = 42 });
        await SetTenantValuesAsync(tenant.Id, Probe, new JsonObject { ["label"] = "mine" });
        var platform = await PlatformClientAsync();

        var (response, _) = await platform.DELETEAsync<SettingDeleteEndpoint, SettingDeleteRequest, EmptyResponse>(new() { Name = Probe });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await StoredValuesAsync(Probe, null)).Should().BeNull();
        (await StoredValuesAsync(Probe, tenant.Id)).Should().NotBeNull("a platform delete removes the platform's row alone");

        var (_, platformList) = await platform.GETAsync<SettingListEndpoint, SettingListResponse>();
        PropertyOf(platformList, Probe, "limit").Value!.GetValue<int>().Should().Be(10);
        PropertyOf(platformList, Probe, "limit").Source.Should().Be("default");

        var tenantClient = await TenantClientAsync(tenant.Id);
        var (_, tenantList) = await tenantClient.GETAsync<SettingListEndpoint, SettingListResponse>();
        PropertyOf(tenantList, Probe, "limit").Source.Should().Be("default");
        PropertyOf(tenantList, Probe, "label").Source.Should().Be("tenant");
    }

    [Fact]
    public async Task Delete_With_Nothing_To_Remove_Still_Succeeds()
    {
        var tenant = await CreateTenantAsync();
        var client = await TenantClientAsync(tenant.Id);

        var (response, _) = await client.DELETEAsync<SettingDeleteEndpoint, SettingDeleteRequest, EmptyResponse>(new() { Name = Probe });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Unknown_Setting_Is_Not_Found()
    {
        var tenant = await CreateTenantAsync();
        var client = await TenantClientAsync(tenant.Id);

        var (response, refusal) = await client.DELETEAsync<SettingDeleteEndpoint, SettingDeleteRequest, ProblemDetails>(
            new() { Name = $"NoSuchSetting{Guid.NewGuid():N}" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        refusal.Errors.Select(e => e.Code).Should().Contain(ErrorCodes.SettingNotFound.Value);
    }

    [Fact]
    public async Task Caller_With_Only_Settings_View_Is_Forbidden()
    {
        var tenant = await CreateTenantAsync();
        await SetTenantValuesAsync(tenant.Id, Probe, new JsonObject { ["limit"] = 7 });
        var client = await TenantClientAsync(tenant.Id, Allow.Settings_View);

        var (response, _) = await client.DELETEAsync<SettingDeleteEndpoint, SettingDeleteRequest, ProblemDetails>(new() { Name = Probe });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await StoredValuesAsync(Probe, tenant.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task Delete_Revokes_No_Session()
    {
        var tenant = await CreateTenantAsync();
        await SetTenantValuesAsync(tenant.Id, Probe, new JsonObject { ["limit"] = 7 });
        var client = await TenantClientAsync(tenant.Id);

        (await client.DELETEAsync<SettingDeleteEndpoint, SettingDeleteRequest, EmptyResponse>(new() { Name = Probe }))
            .Response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await client.GETAsync<SettingListEndpoint, SettingListResponse>()).Response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
