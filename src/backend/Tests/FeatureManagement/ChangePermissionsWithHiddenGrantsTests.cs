namespace Backend.Tests.FeatureManagement;

using Backend.Features.Identity.Endpoints.Roles;

/// <summary>
/// The role permission form is built from a catalogue the tenant's plan has already narrowed, so a
/// grant the plan hides is absent from everything the form submits. These tests pin that saving the
/// form does not read that silence as "remove it".
/// </summary>
/// <remarks>
/// Without this, switching a feature off and then saving any role would destroy the grants that
/// feature hides - and switching the feature back on would restore nothing, because a deleted grant is
/// gone. The grant is the durable fact; the feature decides only whether it is exercised.
/// </remarks>
public class ChangePermissionsWithHiddenGrantsTests(App app) : FeatureTestsBase(app)
{
    [Fact]
    public async Task A_Grant_The_Plan_Hides_Survives_A_Replacement_That_Does_Not_Mention_It()
    {
        var tenant = await CreateTenantAsync();
        var role = await CreateTenantRoleAsync(tenant.Id, Allow.User_View, Allow.File_Delete);
        await SetForTenantAsync(tenant.Id, FeatureNames.FileManagement_Enabled, "false");
        await SignInAsTenantAdministratorAsync(tenant.Id);

        var userView = await PermissionIdAsync(Allow.User_View);
        var response = await ReplacePermissionsAsync(role, [userView]);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await GrantedPermissionNamesAsync(role)).Should().Contain(Allow.File_Delete,
            "the form could not show it, so submitting the form said nothing about it");
    }

    [Fact]
    public async Task A_Grant_The_Plan_Hides_Comes_Back_Into_Force_When_The_Feature_Returns()
    {
        var tenant = await CreateTenantAsync();
        var role = await CreateTenantRoleAsync(tenant.Id, Allow.User_View, Allow.File_Delete);
        await SetForTenantAsync(tenant.Id, FeatureNames.FileManagement_Enabled, "false");
        await SignInAsTenantAdministratorAsync(tenant.Id);

        var userView = await PermissionIdAsync(Allow.User_View);
        await ReplacePermissionsAsync(role, [userView]);
        await SetForTenantAsync(tenant.Id, FeatureNames.FileManagement_Enabled, "true");

        var permitted = await PermissionFeatureFilter.EnabledPermissionNamesAsync(
            FeatureTarget.ForTenant(tenant.Id), TestContext.Current.CancellationToken);

        permitted.Should().Contain(Allow.File_Delete);
        (await GrantedPermissionNamesAsync(role)).Should().Contain(Allow.File_Delete,
            "nothing had to be re-granted, because nothing was taken away");
    }

    [Fact]
    public async Task A_Visible_Grant_Is_Still_Removed_When_The_Replacement_Leaves_It_Out()
    {
        var tenant = await CreateTenantAsync();
        var role = await CreateTenantRoleAsync(tenant.Id, Allow.User_View, Allow.User_Update);
        await SignInAsTenantAdministratorAsync(tenant.Id);

        var userView = await PermissionIdAsync(Allow.User_View);
        var response = await ReplacePermissionsAsync(role, [userView]);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await GrantedPermissionNamesAsync(role)).Should().NotContain(Allow.User_Update,
            "carrying hidden grants through must not turn the replacement into an append");
    }

    #region Helpers

    /// <summary>
    /// Signs in as an account of the tenant holding exactly the permission this surface requires.
    /// </summary>
    private async Task SignInAsTenantAdministratorAsync(Guid tenantId)
    {
        var editorRole = await CreateTenantRoleAsync(tenantId, Allow.Role_View, Allow.Role_ChangePermissions);
        var account = await CreateTenantUserAsync(tenantId, editorRole);
        await SignInAsAsync(account.Username, tenantId);
    }

    private async Task<Guid> PermissionIdAsync(string name)
        => await DbContext.Permissions
            .AsNoTracking()
            .Where(permission => permission.Name == name)
            .Select(permission => permission.Id)
            .SingleAsync(TestContext.Current.CancellationToken);

    private async Task<HttpResponseMessage> ReplacePermissionsAsync(Guid roleId, List<Guid> permissions)
    {
        var (response, _) = await Client
            .PUTAsync<ChangePermissionsEndpoint, ChangePermissionsRequest, ChangePermissionsResponse>(
                new ChangePermissionsRequest { Id = roleId, Permissions = permissions });
        return response;
    }

    private async Task<IReadOnlyList<string>> GrantedPermissionNamesAsync(Guid roleId)
        => await DbContext.RolePermissions
            .AsNoTracking()
            .AcrossAllTenants()
            .Where(rolePermission => rolePermission.RoleId == roleId)
            .Select(rolePermission => rolePermission.Permission.Name)
            .ToListAsync(TestContext.Current.CancellationToken);

    #endregion
}
