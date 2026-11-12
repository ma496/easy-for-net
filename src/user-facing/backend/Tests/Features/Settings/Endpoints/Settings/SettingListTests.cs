using System.Text.Json.Nodes;
using Backend.Features.Settings.Endpoints.Settings;

namespace Backend.Tests.Features.Settings.Endpoints.Settings;

/// <summary>
/// Tests for <see cref="SettingListEndpoint"/>: every declared setting as the acting scope resolves it,
/// each property's value and the layer it came from, and the permission the listing requires.
/// </summary>
public class SettingListTests(App app) : SettingsTestsBase(app)
{
    [Fact]
    public async Task No_Overrides_Lists_Code_Defaults_With_Source_Default()
    {
        var client = await SettingsClientAsync();

        var (response, list) = await client.GETAsync<SettingListEndpoint, SettingListResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var verification = PropertyOf(list, "General", "isEmailVerificationRequired");
        verification.Value!.GetValue<bool>().Should().BeFalse();
        verification.Source.Should().Be("default");

        PropertyOf(list, Probe, "limit").Value!.GetValue<int>().Should().Be(10);
        PropertyOf(list, Probe, "label").Value!.GetValue<string>().Should().Be("default");
        PropertyOf(list, Probe, "enabled").Value!.GetValue<bool>().Should().BeFalse();
        list.Items.Single(x => x.Name == Probe).Properties.Should().OnlyContain(x => x.Source == "default");
    }

    [Fact]
    public async Task Caller_Without_Settings_View_Is_Forbidden()
    {
        var client = await SettingsClientAsync(Allow.Localization_View);

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
