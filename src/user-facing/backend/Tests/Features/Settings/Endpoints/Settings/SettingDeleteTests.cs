using System.Text.Json.Nodes;
using Backend.Features.Settings.Endpoints.Settings;

namespace Backend.Tests.Features.Settings.Endpoints.Settings;

/// <summary>
/// Tests for <see cref="SettingDeleteEndpoint"/>: removing the acting scope's own overrides - and only
/// those - so the setting follows the layer below again, and the permission it requires.
/// </summary>
public class SettingDeleteTests(App app) : SettingsTestsBase(app)
{
    [Fact]
    public async Task Delete_With_Nothing_To_Remove_Still_Succeeds()
    {
        var client = await SettingsClientAsync();

        var (response, _) = await client.DELETEAsync<SettingDeleteEndpoint, SettingDeleteRequest, EmptyResponse>(new() { Name = Probe });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Unknown_Setting_Is_Not_Found()
    {
        var client = await SettingsClientAsync();

        var (response, refusal) = await client.DELETEAsync<SettingDeleteEndpoint, SettingDeleteRequest, ProblemDetails>(
            new() { Name = $"NoSuchSetting{Guid.NewGuid():N}" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        refusal.Errors.Select(e => e.Code).Should().Contain(ErrorCodes.SettingNotFound.Value);
    }

    [Fact]
    public async Task Caller_With_Only_Settings_View_Is_Forbidden()
    {
        await SetStoredValuesAsync(Probe, new JsonObject { ["limit"] = 7 });
        var client = await SettingsClientAsync(Allow.Settings_View);

        var (response, _) = await client.DELETEAsync<SettingDeleteEndpoint, SettingDeleteRequest, ProblemDetails>(new() { Name = Probe });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await StoredValuesAsync(Probe)).Should().NotBeNull();
    }

    [Fact]
    public async Task Delete_Revokes_No_Session()
    {
        await SetStoredValuesAsync(Probe, new JsonObject { ["limit"] = 7 });
        var client = await SettingsClientAsync();

        (await client.DELETEAsync<SettingDeleteEndpoint, SettingDeleteRequest, EmptyResponse>(new() { Name = Probe }))
            .Response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await client.GETAsync<SettingListEndpoint, SettingListResponse>()).Response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
