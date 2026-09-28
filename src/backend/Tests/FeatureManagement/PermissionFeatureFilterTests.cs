namespace Backend.Tests.FeatureManagement;

/// <summary>
/// Pins the rule that decides which permissions a tenant's plan leaves exercisable. Every other part
/// of the system asks this one question, so these cases are what keeps the claims a session carries,
/// the catalogue a role is edited from and the grants the web app is told about in agreement.
/// </summary>
public class PermissionFeatureFilterTests(App app) : FeatureTestsBase(app)
{
    [Fact]
    public async Task With_Every_Feature_On_Nothing_Is_Removed()
    {
        var tenant = await CreateTenantAsync();

        var permitted = await PermittedAsync(tenant.Id);

        permitted.Should().Contain(Allow.File_Delete).And.Contain(Allow.User_Create);
    }

    [Fact]
    public async Task A_Permission_Whose_Feature_Is_Off_Is_Removed()
    {
        var tenant = await CreateTenantAsync();
        await SetForTenantAsync(tenant.Id, FeatureNames.Identity_UserManagement, "false");

        var permitted = await PermittedAsync(tenant.Id);

        permitted.Should().NotContain(Allow.User_Create)
            .And.NotContain(Allow.User_View)
            .And.NotContain(Allow.Role_ChangePermissions);
        permitted.Should().Contain(Allow.File_Delete,
            "only the permissions that require the switched-off feature are gated");
    }

    [Fact]
    public async Task A_Requirement_Declared_On_A_Group_Reaches_Every_Permission_Beneath_It()
    {
        var tenant = await CreateTenantAsync();
        await SetForTenantAsync(tenant.Id, FeatureNames.FileManagement_Enabled, "false");

        var permitted = await PermittedAsync(tenant.Id);

        permitted.Should().NotContain(Allow.File_Delete,
            "the Files group declares the requirement once and every permission under it inherits it");
    }

    [Fact]
    public async Task An_Edition_Can_Withhold_A_Permission_From_Every_Tenant_On_It()
    {
        var edition = await CreateEditionAsync();
        var tenant = await CreateTenantOnEditionAsync(edition.Id);
        await SetForEditionAsync(edition.Id, FeatureNames.FileManagement_Enabled, "false");

        (await PermittedAsync(tenant.Id)).Should().NotContain(Allow.File_Delete);
    }

    [Fact]
    public async Task A_Tenant_Can_Be_Given_Back_What_Its_Edition_Withholds()
    {
        var edition = await CreateEditionAsync();
        var tenant = await CreateTenantOnEditionAsync(edition.Id);
        await SetForEditionAsync(edition.Id, FeatureNames.FileManagement_Enabled, "false");
        await SetForTenantAsync(tenant.Id, FeatureNames.FileManagement_Enabled, "true");

        (await PermittedAsync(tenant.Id)).Should().Contain(Allow.File_Delete);
    }

    [Fact]
    public async Task The_Platform_Target_Narrows_Nothing_At_All()
    {
        var permitted = await PermissionFeatureFilter.EnabledPermissionNamesAsync(
            FeatureTarget.Platform, TestContext.Current.CancellationToken);

        permitted.Should().BeNull(
            "the platform is inside no plan, so there is nothing to consult - and a null answer tells callers to filter nothing rather than that nothing is permitted");
    }

    [Fact]
    public async Task The_Catalogue_Loses_The_Branches_Whose_Features_Are_Off()
    {
        var tenant = await CreateTenantAsync();
        await SetForTenantAsync(tenant.Id, FeatureNames.FileManagement_Enabled, "false");

        var groups = Service<IPermissionDefinitionService>().GetPermissionGroups(PermissionScope.Tenant);
        var filtered = await PermissionFeatureFilter.FilterGroupsAsync(
            groups, FeatureTarget.ForTenant(tenant.Id), TestContext.Current.CancellationToken);

        LeafNames(filtered).Should().NotContain(Allow.File_Delete);
        filtered.Select(group => group.GroupName).Should().NotContain("File Management",
            "a group left with no permission at all is dropped rather than shown empty");
    }

    [Fact]
    public async Task Narrowing_The_Catalogue_To_A_Scope_Does_Not_Lose_The_Gate()
    {
        var tenant = await CreateTenantAsync();
        await SetForTenantAsync(tenant.Id, FeatureNames.Identity_UserManagement, "false");

        // The scope narrowing rebuilds the tree, so this is the case that catches a copy helper that
        // forgets to carry the feature requirement across - after which the gate would silently vanish
        // for every caller that asks for a scoped catalogue, which is all of them.
        var scoped = Service<IPermissionDefinitionService>().GetPermissionGroups(PermissionScope.Tenant);
        var filtered = await PermissionFeatureFilter.FilterGroupsAsync(
            scoped, FeatureTarget.ForTenant(tenant.Id), TestContext.Current.CancellationToken);

        LeafNames(filtered).Should().NotContain(Allow.User_Create).And.NotContain(Allow.Role_View)
            .And.Contain(Allow.File_Delete);
    }

    #region Helpers

    private async Task<IReadOnlySet<string>> PermittedAsync(Guid tenantId)
        => (await PermissionFeatureFilter.EnabledPermissionNamesAsync(
                FeatureTarget.ForTenant(tenantId), TestContext.Current.CancellationToken))!;

    private static IEnumerable<string> LeafNames(IEnumerable<PermissionGroupDefinition> groups)
        => groups.SelectMany(group => group.Permissions).SelectMany(Leaves);

    private static IEnumerable<string> Leaves(PermissionDefinition permission)
    {
        if (permission.Children.Count == 0)
        {
            yield return permission.Name;
            yield break;
        }
        foreach (var name in permission.Children.SelectMany(Leaves))
        {
            yield return name;
        }
    }

    #endregion
}
