namespace Backend.Tests.Features.Settings.Endpoints.Settings;

using System.Text.Json.Nodes;
using Backend.Features.Settings.Core;
using Backend.Features.Settings.Endpoints.Settings;

/// <summary>
/// Tests for <see cref="SettingUpdateEndpoint"/>: a platform write reaching every tenant that did not
/// override it, a tenant write reaching that tenant alone, the refusals of an unknown setting, property
/// or value, the permission it requires, and that a write revokes no session.
/// </summary>
public class SettingUpdateTests(App app) : SettingsTestsBase(app)
{
    [Fact]
    public async Task Platform_Write_Applies_In_Platform_Scope_And_To_A_Tenant_That_Did_Not_Override()
    {
        var tenant = await CreateTenantAsync();
        var platform = await PlatformClientAsync();

        var (response, updated) = await platform.PUTAsync<SettingUpdateEndpoint, SettingUpdateRequest, SettingDto>(
            new() { Name = Probe, Values = new JsonObject { ["limit"] = 77 } });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        updated.Name.Should().Be(Probe);
        PropertyOf(updated, "limit").Value!.GetValue<int>().Should().Be(77);
        PropertyOf(updated, "limit").Source.Should().Be("platform");
        (await StoredValuesAsync(Probe, null))!["limit"]!.GetValue<int>().Should().Be(77);

        var tenantClient = await TenantClientAsync(tenant.Id);
        var (listResponse, list) = await tenantClient.GETAsync<SettingListEndpoint, SettingListResponse>();

        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        PropertyOf(list, Probe, "limit").Value!.GetValue<int>().Should().Be(77);
        PropertyOf(list, Probe, "limit").Source.Should().Be("platform");
        PropertyOf(list, Probe, "label").Source.Should().Be("default");
    }

    [Fact]
    public async Task Tenant_Write_Wins_For_That_Tenant_Only_And_Its_Other_Properties_Follow_The_Platform()
    {
        var overriding = await CreateTenantAsync();
        var other = await CreateTenantAsync();
        var platform = await PlatformClientAsync();
        (await platform.PUTAsync<SettingUpdateEndpoint, SettingUpdateRequest, SettingDto>(
            new() { Name = Probe, Values = new JsonObject { ["limit"] = 42, ["label"] = "platform" } }))
            .Response.StatusCode.Should().Be(HttpStatusCode.OK);

        var overridingClient = await TenantClientAsync(overriding.Id);
        var (response, updated) = await overridingClient.PUTAsync<SettingUpdateEndpoint, SettingUpdateRequest, SettingDto>(
            new() { Name = Probe, Values = new JsonObject { ["limit"] = 7 } });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        PropertyOf(updated, "limit").Value!.GetValue<int>().Should().Be(7);
        PropertyOf(updated, "limit").Source.Should().Be("tenant");
        PropertyOf(updated, "label").Value!.GetValue<string>().Should().Be("platform", "a property the tenant did not override follows the platform");
        PropertyOf(updated, "label").Source.Should().Be("platform");
        PropertyOf(updated, "enabled").Source.Should().Be("default");

        var otherClient = await TenantClientAsync(other.Id);
        var (_, otherList) = await otherClient.GETAsync<SettingListEndpoint, SettingListResponse>();
        PropertyOf(otherList, Probe, "limit").Value!.GetValue<int>().Should().Be(42, "another tenant's override is not this tenant's");
        PropertyOf(otherList, Probe, "limit").Source.Should().Be("platform");
    }

    [Fact]
    public async Task Tenant_Write_Stores_Only_Its_Own_Row_And_Leaves_The_Platform_Row_Unchanged()
    {
        var tenant = await CreateTenantAsync();
        await SetPlatformValuesAsync(Probe, new JsonObject { ["label"] = "platform" });
        var client = await TenantClientAsync(tenant.Id);

        var (response, _) = await client.PUTAsync<SettingUpdateEndpoint, SettingUpdateRequest, SettingDto>(
            new() { Name = Probe, Values = new JsonObject { ["Label"] = "mine", ["enabled"] = true } });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await StoredValuesAsync(Probe, null))!.ToJsonString().Should().Be("""{"label":"platform"}""");

        var own = await StoredValuesAsync(Probe, tenant.Id);
        own!["label"]!.GetValue<string>().Should().Be("mine", "a submitted name is matched case-insensitively and stored camelCase");
        own["enabled"]!.GetValue<bool>().Should().BeTrue();
    }

    [Fact]
    public async Task Write_Replaces_The_Scopes_Overrides_And_An_Empty_Object_Removes_Them()
    {
        var tenant = await CreateTenantAsync();
        var client = await TenantClientAsync(tenant.Id);

        await client.PUTAsync<SettingUpdateEndpoint, SettingUpdateRequest, SettingDto>(
            new() { Name = Probe, Values = new JsonObject { ["limit"] = 5, ["label"] = "first" } });
        var (_, replaced) = await client.PUTAsync<SettingUpdateEndpoint, SettingUpdateRequest, SettingDto>(
            new() { Name = Probe, Values = new JsonObject { ["label"] = "second" } });

        PropertyOf(replaced, "limit").Source.Should().Be("default", "a property left out of the body stops being overridden");
        PropertyOf(replaced, "label").Value!.GetValue<string>().Should().Be("second");

        var (response, cleared) = await client.PUTAsync<SettingUpdateEndpoint, SettingUpdateRequest, SettingDto>(
            new() { Name = Probe, Values = [] });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        cleared.Properties.Should().OnlyContain(x => x.Source == "default");
        (await StoredValuesAsync(Probe, tenant.Id)).Should().BeNull();
    }

    [Fact]
    public async Task Unknown_Setting_Is_Not_Found()
    {
        var tenant = await CreateTenantAsync();
        var client = await TenantClientAsync(tenant.Id);

        var (response, refusal) = await client.PUTAsync<SettingUpdateEndpoint, SettingUpdateRequest, ProblemDetails>(
            new() { Name = $"NoSuchSetting{Guid.NewGuid():N}", Values = new JsonObject { ["limit"] = 1 } });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        refusal.Errors.Select(e => e.Code).Should().Contain(ErrorCodes.SettingNotFound.Value);
    }

    [Fact]
    public async Task Unknown_Property_Is_Rejected()
    {
        var tenant = await CreateTenantAsync();
        var client = await TenantClientAsync(tenant.Id);

        var (response, refusal) = await client.PUTAsync<SettingUpdateEndpoint, SettingUpdateRequest, ProblemDetails>(
            new() { Name = Probe, Values = new JsonObject { ["noSuchProperty"] = 1 } });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        refusal.Errors.Should().Contain(e => e.Code == ErrorCodes.SettingPropertyUnknown.Value && e.Name == "values.noSuchProperty");
        (await StoredValuesAsync(Probe, tenant.Id)).Should().BeNull();
    }

    [Fact]
    public async Task Wrongly_Typed_Value_Is_Rejected()
    {
        var tenant = await CreateTenantAsync();
        var client = await TenantClientAsync(tenant.Id);

        var (response, refusal) = await client.PUTAsync<SettingUpdateEndpoint, SettingUpdateRequest, ProblemDetails>(
            new() { Name = Probe, Values = new JsonObject { ["limit"] = "not a number" } });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        refusal.Errors.Should().Contain(e => e.Code == ErrorCodes.SettingPropertyInvalid.Value && e.Name == "values.limit");
        (await StoredValuesAsync(Probe, tenant.Id)).Should().BeNull();
    }

    [Fact]
    public async Task Value_The_Validator_Refuses_Is_Rejected()
    {
        var tenant = await CreateTenantAsync();
        var client = await TenantClientAsync(tenant.Id);

        var (response, refusal) = await client.PUTAsync<SettingUpdateEndpoint, SettingUpdateRequest, ProblemDetails>(
            new() { Name = Probe, Values = new JsonObject { ["limit"] = 0 } });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        refusal.Errors.Select(e => e.Code).Should().Contain(ErrorCodes.SettingValueInvalid.Value);
        refusal.Errors.Should().Contain(e => e.Name == "values.limit");
        (await StoredValuesAsync(Probe, tenant.Id)).Should().BeNull();
    }

    [Fact]
    public async Task Tenant_Value_Is_Validated_Against_What_It_Would_Resolve_To_Including_The_Platform_Layer()
    {
        var tenant = await CreateTenantAsync();
        await SetPlatformValuesAsync(Probe, new JsonObject { ["label"] = "" });
        var client = await TenantClientAsync(tenant.Id);

        var (response, refusal) = await client.PUTAsync<SettingUpdateEndpoint, SettingUpdateRequest, ProblemDetails>(
            new() { Name = Probe, Values = new JsonObject { ["limit"] = 5 } });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, "the platform's empty label is part of what the tenant would resolve to");
        refusal.Errors.Select(e => e.Code).Should().Contain(ErrorCodes.SettingValueInvalid.Value);
        refusal.Errors.Should().Contain(e => e.Name == "values.label");
    }

    [Fact]
    public async Task Caller_With_Only_Settings_View_Is_Forbidden()
    {
        var tenant = await CreateTenantAsync();
        var client = await TenantClientAsync(tenant.Id, Allow.Settings_View);

        var (response, _) = await client.PUTAsync<SettingUpdateEndpoint, SettingUpdateRequest, ProblemDetails>(
            new() { Name = Probe, Values = new JsonObject { ["limit"] = 5 } });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await StoredValuesAsync(Probe, tenant.Id)).Should().BeNull();
    }

    [Fact]
    public async Task Write_Revokes_No_Session()
    {
        var tenant = await CreateTenantAsync();
        var tenantClient = await TenantClientAsync(tenant.Id);
        var platform = await PlatformClientAsync();

        (await tenantClient.PUTAsync<SettingUpdateEndpoint, SettingUpdateRequest, SettingDto>(
            new() { Name = "Signin", Values = new JsonObject { ["isEmailVerificationRequired"] = false } }))
            .Response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await platform.PUTAsync<SettingUpdateEndpoint, SettingUpdateRequest, SettingDto>(
            new() { Name = Probe, Values = new JsonObject { ["limit"] = 3 } }))
            .Response.StatusCode.Should().Be(HttpStatusCode.OK);

        (await tenantClient.GETAsync<SettingListEndpoint, SettingListResponse>()).Response.StatusCode
            .Should().Be(HttpStatusCode.OK, "the tenant caller's session outlives its own settings change");
        (await platform.GETAsync<SettingListEndpoint, SettingListResponse>()).Response.StatusCode
            .Should().Be(HttpStatusCode.OK, "the platform caller's session outlives its own settings change");
    }
}
