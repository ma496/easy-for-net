namespace Backend.Tests.FeatureManagement;

/// <summary>
/// Pins the fallback order - tenant, then edition, then configuration, then the declared default -
/// and the rule that a child is off whenever an ancestor toggle is off.
/// </summary>
/// <remarks>
/// Each test makes its own tenant and edition, so nothing here changes what another test's session is
/// minted with and the class needs no collection of its own.
/// </remarks>
public class FeatureValueResolutionTests(App app) : FeatureTestsBase(app)
{
    [Fact]
    public async Task With_Nothing_Stored_Every_Feature_Takes_Its_Declared_Default()
    {
        var tenant = await CreateTenantAsync();

        var values = await ResolveForTenantAsync(tenant.Id);

        values.GetOrNull(FeatureNames.FileManagement_Enabled).Should().Be(BooleanValidator.TrueValue);
        values.ProviderOf(FeatureNames.FileManagement_Enabled).Should().Be(FeatureValueProviderNames.Default);
        values.GetOrNull(FeatureNames.Identity_MaxUserCount).Should().Be("100");
    }

    [Fact]
    public async Task An_Edition_Value_Beats_The_Declared_Default()
    {
        var edition = await CreateEditionAsync();
        var tenant = await CreateTenantOnEditionAsync(edition.Id);
        await SetForEditionAsync(edition.Id, FeatureNames.Identity_MaxUserCount, "500");

        var values = await ResolveForTenantAsync(tenant.Id);

        values.GetOrNull(FeatureNames.Identity_MaxUserCount).Should().Be("500");
        values.ProviderOf(FeatureNames.Identity_MaxUserCount).Should().Be(FeatureValueProviderNames.Edition);
    }

    [Fact]
    public async Task A_Tenant_Value_Beats_Its_Edition()
    {
        var edition = await CreateEditionAsync();
        var tenant = await CreateTenantOnEditionAsync(edition.Id);
        await SetForEditionAsync(edition.Id, FeatureNames.Identity_MaxUserCount, "500");
        await SetForTenantAsync(tenant.Id, FeatureNames.Identity_MaxUserCount, "1000");

        var values = await ResolveForTenantAsync(tenant.Id);

        values.GetOrNull(FeatureNames.Identity_MaxUserCount).Should().Be("1000");
        values.ProviderOf(FeatureNames.Identity_MaxUserCount).Should().Be(FeatureValueProviderNames.Tenant);
    }

    [Fact]
    public async Task A_Tenant_On_No_Edition_Skips_The_Edition_Link_Entirely()
    {
        var edition = await CreateEditionAsync();
        await SetForEditionAsync(edition.Id, FeatureNames.Identity_MaxUserCount, "500");
        var tenant = await CreateTenantAsync();

        var values = await ResolveForTenantAsync(tenant.Id);

        values.GetOrNull(FeatureNames.Identity_MaxUserCount).Should().Be("100",
            "a plan it is not on cannot grant it anything");
    }

    [Fact]
    public async Task A_Soft_Deleted_Edition_Stops_Granting_What_It_Granted()
    {
        var edition = await CreateEditionAsync();
        var tenant = await CreateTenantOnEditionAsync(edition.Id);
        await SetForEditionAsync(edition.Id, FeatureNames.Identity_MaxUserCount, "500");

        var stored = await DbContext.Editions.SingleAsync(row => row.Id == edition.Id,
                                                          TestContext.Current.CancellationToken);
        stored.IsDeleted = true;
        await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var values = await ResolveForTenantAsync(tenant.Id);

        values.GetOrNull(FeatureNames.Identity_MaxUserCount).Should().Be("100",
            "the tenant falls through to the declared default, which is why deleting an edition in use is refused");
    }

    [Fact]
    public async Task The_Platform_Target_Resolves_Through_Configuration_And_Defaults_Alone()
    {
        var tenant = await CreateTenantAsync();
        await SetForTenantAsync(tenant.Id, FeatureNames.FileManagement_Enabled, "false");

        var values = await FeatureValueResolver.ResolveAsync(FeatureTarget.Platform,
                                                             TestContext.Current.CancellationToken);

        values.GetOrNull(FeatureNames.FileManagement_Enabled).Should().Be(BooleanValidator.TrueValue);
        values.IsEnabled(FeatureNames.FileManagement_Enabled).Should().BeTrue(
            "the platform is inside nobody's plan, so no tenant's override reaches it");
    }

    [Fact]
    public async Task A_Child_Is_Off_Whenever_Its_Parent_Toggle_Is_Off()
    {
        var tenant = await CreateTenantAsync();
        await SetForTenantAsync(tenant.Id, FeatureNames.Identity_UserManagement, "false");

        var values = await ResolveForTenantAsync(tenant.Id);

        values.IsEnabled(FeatureNames.Identity_UserManagement).Should().BeFalse();
        values.GetOrNull(FeatureNames.Identity_MaxUserCount).Should().Be("100",
            "the child still has a value of its own");
        values.IsEnabled(FeatureNames.Identity_MaxUserCount).Should().BeFalse(
            "but it is not in force, because the feature it refines is switched off");
    }

    [Fact]
    public async Task A_Feature_Nothing_Declares_Is_Neither_Resolved_Nor_Enabled()
    {
        var tenant = await CreateTenantAsync();
        await SetForTenantAsync(tenant.Id, "Removed.Feature", BooleanValidator.TrueValue);

        var values = await ResolveForTenantAsync(tenant.Id);

        values.GetOrNull("Removed.Feature").Should().BeNull();
        values.IsEnabled("Removed.Feature").Should().BeFalse();
    }

    [Fact]
    public async Task A_Value_Written_Earlier_In_The_Same_Scope_Is_Visible_To_A_Later_Read()
    {
        var tenant = await CreateTenantAsync();

        (await ResolveForTenantAsync(tenant.Id)).IsEnabled(FeatureNames.FileManagement_Enabled)
            .Should().BeTrue();

        await SetForTenantAsync(tenant.Id, FeatureNames.FileManagement_Enabled, "false");

        (await ResolveForTenantAsync(tenant.Id)).IsEnabled(FeatureNames.FileManagement_Enabled)
            .Should().BeFalse("resolution keeps nothing between calls, so a write is never hidden by a stale read");
    }
}
