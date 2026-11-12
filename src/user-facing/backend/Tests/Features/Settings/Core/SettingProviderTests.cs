using System.Text.Json.Nodes;
using Backend.Features.Identity.Core;
using Backend.Features.Settings.Core;
using Backend.Tests.Fakes;

namespace Backend.Tests.Features.Settings.Core;

/// <summary>
/// Tests for <see cref="ISettingProvider"/>: typed resolution property by property - the stored override,
/// then the code default - and what a stored row can and cannot contribute.
/// </summary>
public class SettingProviderTests(App app) : SettingsTestsBase(app)
{
    private ISettingProvider SettingProvider => Service<ISettingProvider>();

    [Fact]
    public async Task No_Overrides_General_Resolves_To_Its_Code_Default()
    {

        var general = await SettingProvider.GetAsync<GeneralSettings>(TestContext.Current.CancellationToken);

        general.Should().BeEquivalentTo(new GeneralSettings());
    }

    [Fact]
    public async Task No_Overrides_Resolves_To_The_Code_Default()
    {

        var probe = await SettingProvider.GetAsync<ProbeSettings>(TestContext.Current.CancellationToken);

        probe.Should().BeEquivalentTo(new ProbeSettings());
    }

    [Fact]
    public async Task Stored_Properties_The_Class_Lacks_Or_Cannot_Read_Are_Ignored_And_Keys_Match_Any_Case()
    {
        await SetStoredValuesAsync(Probe, new JsonObject
        {
            ["removedLongAgo"] = "x",
            ["limit"] = "not a number",
            ["Label"] = "cased"
        });

        var probe = await SettingProvider.GetAsync<ProbeSettings>(TestContext.Current.CancellationToken);

        probe.Limit.Should().Be(10, "a stored value of the wrong type contributes nothing");
        probe.Label.Should().Be("cased", "stored keys are matched case-insensitively");
    }

    [Fact]
    public async Task Every_Read_Returns_A_New_Instance()
    {

        var first = await SettingProvider.GetAsync<ProbeSettings>(TestContext.Current.CancellationToken);
        first.Limit = 999;
        var second = await SettingProvider.GetAsync<ProbeSettings>(TestContext.Current.CancellationToken);

        second.Should().NotBeSameAs(first);
        second.Limit.Should().Be(10);
    }

    [Fact]
    public async Task Unregistered_Setting_Throws()
    {
        var act = () => SettingProvider.GetAsync<UnregisteredSettings>(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage($"*{nameof(UnregisteredSettings)}*not registered*");
    }

    public sealed class UnregisteredSettings
    {
        public int Value { get; set; }
    }
}