namespace Backend.Tests.Features.Settings.Core;

using System.Text.Json.Nodes;
using Backend.Features.Identity.Core;
using Backend.Features.Settings.Core;
using Backend.Tests.Fakes;

/// <summary>
/// Tests for <see cref="ISettingProvider"/>: typed resolution property by property - tenant override,
/// then platform override, then the code default - and what a stored row can and cannot contribute.
/// </summary>
public class SettingProviderTests(App app) : SettingsTestsBase(app)
{
    private ISettingProvider SettingProvider => Service<ISettingProvider>();

    [Fact]
    public async Task No_Overrides_Signin_Resolves_To_Its_Code_Default()
    {
        var tenant = await CreateTenantAsync();

        var forTenant = await SettingProvider.GetAsync<SigninSettings>(tenant.Id, TestContext.Current.CancellationToken);
        var forPlatform = await SettingProvider.GetAsync<SigninSettings>((Guid?)null, TestContext.Current.CancellationToken);

        forTenant.Should().BeEquivalentTo(new SigninSettings());
        forPlatform.Should().BeEquivalentTo(new SigninSettings());
    }

    [Fact]
    public async Task No_Overrides_Resolves_To_The_Code_Default()
    {
        var tenant = await CreateTenantAsync();

        var probe = await SettingProvider.GetAsync<ProbeSettings>(tenant.Id, TestContext.Current.CancellationToken);

        probe.Should().BeEquivalentTo(new ProbeSettings());
    }

    [Fact]
    public async Task Platform_Override_Applies_To_The_Platform_And_To_Tenants_That_Did_Not_Override()
    {
        var tenant = await CreateTenantAsync();
        await SetPlatformValuesAsync(Probe, new JsonObject { ["limit"] = 42 });

        var forPlatform = await SettingProvider.GetAsync<ProbeSettings>((Guid?)null, TestContext.Current.CancellationToken);
        var forTenant = await SettingProvider.GetAsync<ProbeSettings>(tenant.Id, TestContext.Current.CancellationToken);

        forPlatform.Limit.Should().Be(42);
        forTenant.Limit.Should().Be(42);
        forTenant.Label.Should().Be("default");
    }

    [Fact]
    public async Task Tenant_Override_Wins_For_That_Tenant_And_Its_Other_Properties_Follow_The_Platform()
    {
        var overriding = await CreateTenantAsync();
        var other = await CreateTenantAsync();
        await SetPlatformValuesAsync(Probe, new JsonObject { ["limit"] = 42, ["label"] = "platform" });
        await SetTenantValuesAsync(overriding.Id, Probe, new JsonObject { ["limit"] = 7 });

        var forOverriding = await SettingProvider.GetAsync<ProbeSettings>(overriding.Id, TestContext.Current.CancellationToken);
        var forOther = await SettingProvider.GetAsync<ProbeSettings>(other.Id, TestContext.Current.CancellationToken);

        forOverriding.Limit.Should().Be(7, "the tenant's own override wins");
        forOverriding.Label.Should().Be("platform", "a property the tenant did not override follows the platform");
        forOverriding.Enabled.Should().BeFalse("a property nobody overrode follows the code default");
        forOther.Limit.Should().Be(42, "another tenant's override is not this tenant's");
    }

    [Fact]
    public async Task Acting_Scope_Overload_Reads_The_Tenant_Context()
    {
        var tenant = await CreateTenantAsync();
        await SetTenantValuesAsync(tenant.Id, Probe, new JsonObject { ["label"] = "mine" });

        using (TenantContext.BeginTenant(tenant.Id))
        {
            (await SettingProvider.GetAsync<ProbeSettings>(TestContext.Current.CancellationToken)).Label.Should().Be("mine");
        }

        using (TenantContext.BeginPlatformScope())
        {
            (await SettingProvider.GetAsync<ProbeSettings>(TestContext.Current.CancellationToken)).Label.Should().Be("default");
        }
    }

    [Fact]
    public async Task Stored_Properties_The_Class_Lacks_Or_Cannot_Read_Are_Ignored_And_Keys_Match_Any_Case()
    {
        var tenant = await CreateTenantAsync();
        await SetTenantValuesAsync(tenant.Id, Probe, new JsonObject
        {
            ["removedLongAgo"] = "x",
            ["limit"] = "not a number",
            ["Label"] = "cased"
        });

        var probe = await SettingProvider.GetAsync<ProbeSettings>(tenant.Id, TestContext.Current.CancellationToken);

        probe.Limit.Should().Be(10, "a stored value of the wrong type contributes nothing");
        probe.Label.Should().Be("cased", "stored keys are matched case-insensitively");
    }

    [Fact]
    public async Task Every_Read_Returns_A_New_Instance()
    {
        var tenant = await CreateTenantAsync();

        var first = await SettingProvider.GetAsync<ProbeSettings>(tenant.Id, TestContext.Current.CancellationToken);
        first.Limit = 999;
        var second = await SettingProvider.GetAsync<ProbeSettings>(tenant.Id, TestContext.Current.CancellationToken);

        second.Should().NotBeSameAs(first);
        second.Limit.Should().Be(10);
    }

    [Fact]
    public async Task Values_Are_Cached_For_The_Scope_And_A_Write_Through_It_Is_Seen()
    {
        var tenant = await CreateTenantAsync();
        (await SettingProvider.GetAsync<ProbeSettings>(tenant.Id, TestContext.Current.CancellationToken)).Label.Should().Be("default");

        // Written from another scope: this scope has already read the setting and keeps what it read.
        await using (var otherScope = App.Services.CreateAsyncScope())
        {
            var otherContext = otherScope.ServiceProvider.GetRequiredService<Backend.Features.Tenancy.Core.ITenantContext>();
            using var tenantScope = otherContext.BeginTenant(tenant.Id);
            var dbContext = otherScope.ServiceProvider.GetRequiredService<AppDbContext>();
            dbContext.SettingValues.Add(new() { Name = Probe, Values = """{"label":"elsewhere"}""" });
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        (await SettingProvider.GetAsync<ProbeSettings>(tenant.Id, TestContext.Current.CancellationToken)).Label.Should().Be("default", "a read is cached for the rest of the request");

        // Written through this scope: the cached read is dropped and the next one sees the write.
        var definition = Service<ISettingDefinitionCatalogue>().Get(typeof(ProbeSettings));
        using (TenantContext.BeginTenant(tenant.Id))
        {
            await Service<ISettingValueService>().SetOwnAsync(definition, new JsonObject { ["label"] = "written" }, TestContext.Current.CancellationToken);
        }

        (await SettingProvider.GetAsync<ProbeSettings>(tenant.Id, TestContext.Current.CancellationToken)).Label.Should().Be("written");
        (await Service<ISettingProvider>().GetAsync<ProbeSettings>(tenant.Id, TestContext.Current.CancellationToken)).Label.Should().Be("written");
    }

    [Fact]
    public async Task Unregistered_Setting_Throws()
    {
        var act = () => SettingProvider.GetAsync<UnregisteredSettings>((Guid?)null, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage($"*{nameof(UnregisteredSettings)}*not registered*");
    }

    public sealed class UnregisteredSettings
    {
        public int Value { get; set; }
    }
}