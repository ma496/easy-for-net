namespace Backend.Tests.FeatureManagement;

/// <summary>
/// Pins how stored feature values are written, read, cleared and pruned.
/// </summary>
/// <remarks>
/// Serialised against the rest of the suite because pruning deletes every row whose feature the
/// catalogue no longer declares - across the whole table, since the rows carry no scope to narrow by -
/// and these tests deliberately write such rows.
/// </remarks>
[Collection("FeatureManagement")]
public class FeatureValueStoreTests(App app) : FeatureTestsBase(app)
{
    [Fact]
    public async Task A_Stored_Value_Comes_Back_For_Its_Own_Provider_Key_Alone()
    {
        var tenant = await CreateTenantAsync();
        var other = await CreateTenantAsync();

        await SetForTenantAsync(tenant.Id, FeatureNames.FileManagement_Enabled, "false");

        var mine = await FeatureValueStore.GetAllAsync(FeatureValueProviderNames.Tenant, tenant.Id.ToString(),
                                                       TestContext.Current.CancellationToken);
        var theirs = await FeatureValueStore.GetAllAsync(FeatureValueProviderNames.Tenant, other.Id.ToString(),
                                                         TestContext.Current.CancellationToken);

        mine.Should().Contain(FeatureNames.FileManagement_Enabled, "false");
        theirs.Should().NotContainKey(FeatureNames.FileManagement_Enabled,
            "the provider key is the whole of the attribution, so one tenant's value is not another's");
    }

    [Fact]
    public async Task Setting_A_Value_Twice_Replaces_It_Rather_Than_Adding_A_Second_Row()
    {
        var tenant = await CreateTenantAsync();

        await SetForTenantAsync(tenant.Id, FeatureNames.FileManagement_Enabled, "false");
        await SetForTenantAsync(tenant.Id, FeatureNames.FileManagement_Enabled, "true");

        var values = await FeatureValueStore.GetAllAsync(FeatureValueProviderNames.Tenant, tenant.Id.ToString(),
                                                         TestContext.Current.CancellationToken);

        values.Should().Contain(FeatureNames.FileManagement_Enabled, "true");
        (await DbContext.FeatureValues
            .CountAsync(value => value.ProviderKey == tenant.Id.ToString(), TestContext.Current.CancellationToken))
            .Should().Be(1);
    }

    [Fact]
    public async Task Clearing_A_Value_Deletes_The_Row_So_It_Falls_Through()
    {
        var tenant = await CreateTenantAsync();
        await SetForTenantAsync(tenant.Id, FeatureNames.FileManagement_Enabled, "false");

        await SetForTenantAsync(tenant.Id, FeatureNames.FileManagement_Enabled, null);

        (await FeatureValueStore.GetAllAsync(FeatureValueProviderNames.Tenant, tenant.Id.ToString(),
                                             TestContext.Current.CancellationToken))
            .Should().BeEmpty();
    }

    [Fact]
    public async Task Clearing_A_Value_That_Was_Never_Set_Is_Not_An_Error()
    {
        var tenant = await CreateTenantAsync();

        var act = async () => await SetForTenantAsync(tenant.Id, FeatureNames.FileManagement_Enabled, null);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Deleting_Every_Value_Clears_One_Provider_Key_Only()
    {
        var tenant = await CreateTenantAsync();
        var other = await CreateTenantAsync();
        await SetForTenantAsync(tenant.Id, FeatureNames.FileManagement_Enabled, "false");
        await SetForTenantAsync(tenant.Id, FeatureNames.Identity_MaxUserCount, "5");
        await SetForTenantAsync(other.Id, FeatureNames.FileManagement_Enabled, "false");

        await FeatureValueStore.DeleteAllAsync(FeatureValueProviderNames.Tenant, tenant.Id.ToString(),
                                               TestContext.Current.CancellationToken);

        (await FeatureValueStore.GetAllAsync(FeatureValueProviderNames.Tenant, tenant.Id.ToString(),
                                             TestContext.Current.CancellationToken))
            .Should().BeEmpty();
        (await FeatureValueStore.GetAllAsync(FeatureValueProviderNames.Tenant, other.Id.ToString(),
                                             TestContext.Current.CancellationToken))
            .Should().ContainKey(FeatureNames.FileManagement_Enabled);
    }

    [Fact]
    public async Task Pruning_Removes_Values_Nothing_Declares_And_Keeps_The_Rest()
    {
        var tenant = await CreateTenantAsync();
        await SetForTenantAsync(tenant.Id, FeatureNames.FileManagement_Enabled, "false");
        await SetForTenantAsync(tenant.Id, "Removed.Feature", "true");

        var removed = await FeatureValueStore.PruneUnknownAsync(FeatureDefinitions.GetNames(),
                                                                TestContext.Current.CancellationToken);

        removed.Should().BeGreaterThanOrEqualTo(1);
        var values = await FeatureValueStore.GetAllAsync(FeatureValueProviderNames.Tenant, tenant.Id.ToString(),
                                                         TestContext.Current.CancellationToken);
        values.Should().ContainKey(FeatureNames.FileManagement_Enabled)
              .And.NotContainKey("Removed.Feature");
    }
}
