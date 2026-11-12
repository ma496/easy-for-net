using System.Text.Json.Nodes;
using Backend.Features.Settings.Core;
using Backend.Features.Settings.Endpoints.Settings;

namespace Backend.Tests.Features.Settings.Endpoints.Settings;

/// <summary>
/// Tests for <see cref="SettingUpdateEndpoint"/>: the refusals of an unknown setting, property
/// or value, the permission it requires, and that a write revokes no session.
/// </summary>
public class SettingUpdateTests(App app) : SettingsTestsBase(app)
{
    [Fact]
    public async Task Unknown_Setting_Is_Not_Found()
    {
        var client = await SettingsClientAsync();

        var (response, refusal) = await client.PUTAsync<SettingUpdateEndpoint, SettingUpdateRequest, ProblemDetails>(
            new() { Name = $"NoSuchSetting{Guid.NewGuid():N}", Values = new JsonObject { ["limit"] = 1 } });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        refusal.Errors.Select(e => e.Code).Should().Contain(ErrorCodes.SettingNotFound.Value);
    }

    [Fact]
    public async Task Unknown_Property_Is_Rejected()
    {
        var client = await SettingsClientAsync();

        var (response, refusal) = await client.PUTAsync<SettingUpdateEndpoint, SettingUpdateRequest, ProblemDetails>(
            new() { Name = Probe, Values = new JsonObject { ["noSuchProperty"] = 1 } });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        refusal.Errors.Should().Contain(e => e.Code == ErrorCodes.SettingPropertyUnknown.Value && e.Name == "values.noSuchProperty");
        (await StoredValuesAsync(Probe)).Should().BeNull();
    }

    [Fact]
    public async Task Wrongly_Typed_Value_Is_Rejected()
    {
        var client = await SettingsClientAsync();

        var (response, refusal) = await client.PUTAsync<SettingUpdateEndpoint, SettingUpdateRequest, ProblemDetails>(
            new() { Name = Probe, Values = new JsonObject { ["limit"] = "not a number" } });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        refusal.Errors.Should().Contain(e => e.Code == ErrorCodes.SettingPropertyInvalid.Value && e.Name == "values.limit");
        (await StoredValuesAsync(Probe)).Should().BeNull();
    }

    [Fact]
    public async Task Value_The_Validator_Refuses_Is_Rejected()
    {
        var client = await SettingsClientAsync();

        var (response, refusal) = await client.PUTAsync<SettingUpdateEndpoint, SettingUpdateRequest, ProblemDetails>(
            new() { Name = Probe, Values = new JsonObject { ["limit"] = 0 } });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        refusal.Errors.Select(e => e.Code).Should().Contain(ErrorCodes.SettingValueInvalid.Value);
        refusal.Errors.Should().Contain(e => e.Name == "values.limit");
        (await StoredValuesAsync(Probe)).Should().BeNull();
    }

    [Fact]
    public async Task Caller_With_Only_Settings_View_Is_Forbidden()
    {
        var client = await SettingsClientAsync(Allow.Settings_View);

        var (response, _) = await client.PUTAsync<SettingUpdateEndpoint, SettingUpdateRequest, ProblemDetails>(
            new() { Name = Probe, Values = new JsonObject { ["limit"] = 5 } });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await StoredValuesAsync(Probe)).Should().BeNull();
    }

    [Fact]
    public async Task Write_Revokes_No_Session()
    {
        var settingsClient = await SettingsClientAsync();
        var admin = await AdminClientAsync();

        (await settingsClient.PUTAsync<SettingUpdateEndpoint, SettingUpdateRequest, SettingDto>(
            new() { Name = "General", Values = new JsonObject { ["isEmailVerificationRequired"] = false } }))
            .Response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.PUTAsync<SettingUpdateEndpoint, SettingUpdateRequest, SettingDto>(
            new() { Name = Probe, Values = new JsonObject { ["limit"] = 3 } }))
            .Response.StatusCode.Should().Be(HttpStatusCode.OK);

        (await settingsClient.GETAsync<SettingListEndpoint, SettingListResponse>()).Response.StatusCode
            .Should().Be(HttpStatusCode.OK, "the settings caller's session outlives its own settings change");
        (await admin.GETAsync<SettingListEndpoint, SettingListResponse>()).Response.StatusCode
            .Should().Be(HttpStatusCode.OK, "the administrator's session outlives its own settings change");
    }
}
