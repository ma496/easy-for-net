namespace Backend.Tests.Features.Identity.Endpoints.Roles;

using Backend.Features.Identity.Endpoints.Roles;

/// <summary>
/// Tests that <see cref="ChangePermissionsEndpoint"/> and <see cref="RoleDeleteEndpoint"/> end the sessions
/// of the accounts holding the role, in the role's own tenant only, and that
/// <see cref="RoleUpdateEndpoint"/>, which grants nothing, ends none.
/// </summary>
/// <remarks>
/// Each test builds two tenants, so the account that holds a role of the first tenant while also being a
/// member of the second shows the scope of a revocation: its session in the first tenant carries the role's
/// grants and ends, its session in the second carries none of them and keeps working.
/// </remarks>
public class RoleSessionRevocationTests(App app) : SessionRevocationTestsBase(app)
{
    /// <summary>
    /// Verifies a change to what a role grants ends the sessions of its holders in the role's tenant, and
    /// leaves an account of another role in that tenant and the holder's own session in another tenant working.
    /// </summary>
    [Fact]
    public async Task Changing_A_Roles_Permissions_Ends_The_Holders_Sessions_In_Its_Tenant()
    {
        var arrangement = await ArrangeAsync(Allow.Role_ChangePermissions);

        var (response, _) = await arrangement.Admin.PUTAsync<ChangePermissionsEndpoint, ChangePermissionsRequest, ChangePermissionsResponse>(
            new()
            {
                Id = arrangement.RoleId,
                Permissions = [await PermissionIdAsync(Allow.Role_View), await PermissionIdAsync(Allow.Role_Update)]
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await AssertEndedAsync(arrangement.HolderFirst);
        await AssertEndedAsync(arrangement.HolderSecond);
        await AssertEndedAsync(arrangement.DualInRoleTenant);
        await AssertAliveAsync(arrangement.OtherRoleUser);
        await AssertAliveAsync(arrangement.DualInOtherTenant);
    }

    /// <summary>
    /// Verifies resubmitting the set a role already holds changes nothing, so it ends nothing.
    /// </summary>
    [Fact]
    public async Task Resubmitting_The_Same_Permissions_Revokes_Nothing()
    {
        var arrangement = await ArrangeAsync(Allow.Role_ChangePermissions);

        var (response, _) = await arrangement.Admin.PUTAsync<ChangePermissionsEndpoint, ChangePermissionsRequest, ChangePermissionsResponse>(
            new() { Id = arrangement.RoleId, Permissions = [await PermissionIdAsync(Allow.Role_View)] });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await AssertAliveAsync(arrangement.HolderFirst);
        await AssertAliveAsync(arrangement.HolderSecond);
        await AssertAliveAsync(arrangement.DualInRoleTenant);
    }

    /// <summary>
    /// Verifies a refused permission change - a platform permission on a tenant role - ends no session.
    /// </summary>
    [Fact]
    public async Task A_Refused_Permission_Change_Leaves_Sessions_Working()
    {
        var arrangement = await ArrangeAsync(Allow.Role_ChangePermissions);

        var (response, problem) = await arrangement.Admin.PUTAsync<ChangePermissionsEndpoint, ChangePermissionsRequest, ProblemDetails>(
            new() { Id = arrangement.RoleId, Permissions = [await PermissionIdAsync(Allow.Tenant_Create)] });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.First().Code.Should().Be(ErrorCodes.PlatformPermissionNotGrantable.Value);
        await AssertAliveAsync(arrangement.HolderFirst);
        await AssertAliveAsync(arrangement.DualInRoleTenant);
    }

    /// <summary>
    /// Verifies deleting a role ends the sessions of its holders in the role's tenant, and leaves an account
    /// of another role and the holder's own session in another tenant working.
    /// </summary>
    [Fact]
    public async Task Deleting_A_Role_Ends_The_Holders_Sessions_In_Its_Tenant()
    {
        var arrangement = await ArrangeAsync(Allow.Role_Delete);

        var (response, _) = await arrangement.Admin.DELETEAsync<RoleDeleteEndpoint, RoleDeleteRequest, RoleDeleteResponse>(
            new() { Id = arrangement.RoleId });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await AssertEndedAsync(arrangement.HolderFirst);
        await AssertEndedAsync(arrangement.HolderSecond);
        await AssertEndedAsync(arrangement.DualInRoleTenant);
        await AssertAliveAsync(arrangement.OtherRoleUser);
        await AssertAliveAsync(arrangement.DualInOtherTenant);
    }

    /// <summary>
    /// Verifies renaming a role, which grants nothing, ends no session.
    /// </summary>
    [Fact]
    public async Task Renaming_A_Role_Revokes_Nothing()
    {
        var arrangement = await ArrangeAsync(Allow.Role_Update);

        var (response, _) = await arrangement.Admin.PUTAsync<RoleUpdateEndpoint, RoleUpdateRequest, RoleUpdateResponse>(
            new() { Id = arrangement.RoleId, Name = $"Renamed {Guid.NewGuid():N}", Description = "Renamed by a test" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await AssertAliveAsync(arrangement.HolderFirst);
        await AssertAliveAsync(arrangement.HolderSecond);
        await AssertAliveAsync(arrangement.DualInRoleTenant);
        await AssertAliveAsync(arrangement.OtherRoleUser);
    }

    /// <summary>
    /// What a test starts from: a role and its holders in one tenant, an account of another role there, and
    /// an account that holds the role while also being a member of a second tenant.
    /// </summary>
    private sealed record Arrangement(
        HttpClient Admin,
        Guid RoleId,
        RenewableSession HolderFirst,
        RenewableSession HolderSecond,
        RenewableSession OtherRoleUser,
        RenewableSession DualInRoleTenant,
        RenewableSession DualInOtherTenant);

    /// <summary>
    /// Builds the tenants, the accounts and their sessions.
    /// </summary>
    /// <param name="administratorPermission">The one permission the tenant's administrator holds.</param>
    private async Task<Arrangement> ArrangeAsync(string administratorPermission)
    {
        var tenant = await CreateTenantAsync();
        var otherTenant = await CreateTenantAsync();

        var administrator = await CreateTenantUserAsync(
            tenant.Id, await CreateTenantRoleAsync(tenant.Id, administratorPermission));
        var admin = await ClientForAsync(administrator.Username, tenant.Id);

        var roleId = await CreateTenantRoleAsync(tenant.Id, Allow.Role_View);
        var otherRoleId = await CreateTenantRoleAsync(tenant.Id, Allow.User_View);

        var holder = await CreateTenantUserAsync(tenant.Id, roleId);
        var otherRoleUser = await CreateTenantUserAsync(tenant.Id, otherRoleId);
        var dual = await CreateDualTenantMemberAsync(tenant.Id, otherTenant.Id);
        await TenantAuthorizationService.ReplaceTenantRoleAssignmentsAsync(tenant.Id, dual.Id, [roleId]);

        return new Arrangement(
            admin,
            roleId,
            await SessionForAsync(holder.Username, tenant.Id),
            await SessionForAsync(holder.Username, tenant.Id),
            await SessionForAsync(otherRoleUser.Username, tenant.Id),
            await SessionForAsync(dual.Username, tenant.Id),
            await SessionForAsync(dual.Username, otherTenant.Id));
    }
}
