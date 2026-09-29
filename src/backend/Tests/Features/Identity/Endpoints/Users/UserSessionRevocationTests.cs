namespace Backend.Tests.Features.Identity.Endpoints.Users;

using Backend.Features.Identity.Endpoints.Users;

/// <summary>
/// Tests that <see cref="UserUpdateEndpoint"/> and <see cref="UserDeleteEndpoint"/> end the live sessions
/// their change invalidates - deactivating or deleting an account ends all of them, changing its roles ends
/// those in the scope the roles belong to - and leave every other session, and every refused change, alone.
/// </summary>
/// <remarks>
/// Every tenant, role and account is made by the test, and each tenant is administered by an account of its
/// own holding just the permissions the call needs, so nothing seeded is written and the tests run beside
/// the rest of the suite.
/// </remarks>
public class UserSessionRevocationTests(App app) : SessionRevocationTestsBase(app)
{
    /// <summary>
    /// Verifies deactivating an account ends every session it holds, while another account's session in the
    /// same tenant keeps working.
    /// </summary>
    [Fact]
    public async Task Deactivating_A_User_Ends_All_Its_Sessions()
    {
        var (tenantId, admin, roleId, _) = await ArrangeTenantAsync();
        var target = await CreateTenantUserAsync(tenantId, roleId);
        var first = await SessionForAsync(target.Username, tenantId);
        var second = await SessionForAsync(target.Username, tenantId);
        var bystander = await SessionForAsync((await CreateTenantUserAsync(tenantId, roleId)).Username, tenantId);

        var (response, _) = await admin.PUTAsync<UserUpdateEndpoint, UserUpdateRequest, UserUpdateResponse>(
            new() { Id = target.Id, IsActive = false, Roles = [roleId] });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await AssertEndedAsync(first);
        await AssertEndedAsync(second);
        await AssertAliveAsync(bystander);
    }

    /// <summary>
    /// Verifies deleting an account ends every session it holds, while another account's session keeps working.
    /// </summary>
    [Fact]
    public async Task Deleting_A_User_Ends_All_Its_Sessions()
    {
        var (tenantId, admin, roleId, _) = await ArrangeTenantAsync();
        var target = await CreateTenantUserAsync(tenantId, roleId);
        var first = await SessionForAsync(target.Username, tenantId);
        var second = await SessionForAsync(target.Username, tenantId);
        var bystander = await SessionForAsync((await CreateTenantUserAsync(tenantId, roleId)).Username, tenantId);

        var (response, _) = await admin.DELETEAsync<UserDeleteEndpoint, UserDeleteRequest, UserDeleteResponse>(
            new() { Id = target.Id });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await AssertEndedAsync(first);
        await AssertEndedAsync(second);
        await AssertAliveAsync(bystander);
    }

    /// <summary>
    /// Verifies changing an account's roles inside a tenant ends its sessions in that tenant, while another
    /// account's session keeps working.
    /// </summary>
    [Fact]
    public async Task Changing_A_Users_Roles_Ends_Its_Sessions_In_The_Tenant()
    {
        var (tenantId, admin, roleId, otherRoleId) = await ArrangeTenantAsync();
        var target = await CreateTenantUserAsync(tenantId, roleId);
        var first = await SessionForAsync(target.Username, tenantId);
        var second = await SessionForAsync(target.Username, tenantId);
        var bystander = await SessionForAsync((await CreateTenantUserAsync(tenantId, roleId)).Username, tenantId);

        var (response, _) = await admin.PUTAsync<UserUpdateEndpoint, UserUpdateRequest, UserUpdateResponse>(
            new() { Id = target.Id, IsActive = true, Roles = [otherRoleId] });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await AssertEndedAsync(first);
        await AssertEndedAsync(second);
        await AssertAliveAsync(bystander);
    }

    /// <summary>
    /// Verifies an update that changes neither the account's active state nor its roles - a rename of the
    /// person, say - ends nothing, since nothing a session was minted from moved.
    /// </summary>
    [Fact]
    public async Task Updating_Only_The_Name_Revokes_Nothing()
    {
        var (tenantId, admin, roleId, _) = await ArrangeTenantAsync();
        var target = await CreateTenantUserAsync(tenantId, roleId);
        var session = await SessionForAsync(target.Username, tenantId);

        var (response, _) = await admin.PUTAsync<UserUpdateEndpoint, UserUpdateRequest, UserUpdateResponse>(
            new() { Id = target.Id, IsActive = true, FirstName = "Renamed", LastName = "Person", Roles = [roleId] });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await AssertAliveAsync(session);
    }

    /// <summary>
    /// Verifies changing the roles of a platform account in platform scope ends its platform-scope session and
    /// leaves the session it holds inside a tenant it is a member of, which those roles do not shape.
    /// </summary>
    [Fact]
    public async Task Changing_A_Platform_Accounts_Roles_Ends_Only_Its_Platform_Scope_Sessions()
    {
        var tenant = await CreateTenantAsync();
        var account = await CreateTenantUserAsync(tenant.Id);
        await MarkAsPlatformAccountAsync(account.Id);
        var heldRoleId = await CreatePlatformRoleAsync();
        var newRoleId = await CreatePlatformRoleAsync();
        await UserService.AssignRoleAsync(account.Id, heldRoleId);

        var platformSession = await SessionForAsync(account.Username);
        var tenantSession = await SessionForAsync(account.Username, tenant.Id);
        var bystander = await SessionForAsync((await CreatePlatformAccountAsync()).Username);

        await SetPlatformAdminAuthTokenAsync();
        var (response, _) = await Client.PUTAsync<UserUpdateEndpoint, UserUpdateRequest, UserUpdateResponse>(
            new() { Id = account.Id, IsActive = true, Roles = [newRoleId] });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await AssertEndedAsync(platformSession);
        await AssertAliveAsync(tenantSession);
        await AssertAliveAsync(bystander);
    }

    /// <summary>
    /// Verifies a refused update - a role of another tenant answers <see cref="ErrorCodes.ReferencedRecordNotFound"/> -
    /// ends no session.
    /// </summary>
    [Fact]
    public async Task A_Refused_Update_Leaves_Sessions_Working()
    {
        var (tenantId, admin, roleId, _) = await ArrangeTenantAsync();
        var strangerRoleId = await CreateTenantRoleAsync((await CreateTenantAsync()).Id, Allow.User_View);
        var target = await CreateTenantUserAsync(tenantId, roleId);
        var first = await SessionForAsync(target.Username, tenantId);
        var second = await SessionForAsync(target.Username, tenantId);

        var (response, problem) = await admin.PUTAsync<UserUpdateEndpoint, UserUpdateRequest, ProblemDetails>(
            new() { Id = target.Id, IsActive = false, Roles = [strangerRoleId] });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.First().Code.Should().Be(ErrorCodes.ReferencedRecordNotFound.Value);
        await AssertAliveAsync(first);
        await AssertAliveAsync(second);
    }

    /// <summary>
    /// Verifies an update the tenant's administrator may not make - the account is shared with another
    /// tenant, so it is refused with <see cref="ErrorCodes.UserSharedAcrossTenants"/> - ends the account's
    /// sessions in neither tenant.
    /// </summary>
    [Fact]
    public async Task A_Refused_Update_Of_A_Shared_Account_Leaves_Its_Sessions_In_Both_Tenants_Working()
    {
        var (tenantId, admin, roleId, _) = await ArrangeTenantAsync();
        var otherTenant = await CreateTenantAsync();
        var shared = await CreateDualTenantMemberAsync(tenantId, otherTenant.Id);
        var here = await SessionForAsync(shared.Username, tenantId);
        var there = await SessionForAsync(shared.Username, otherTenant.Id);

        var (response, problem) = await admin.PUTAsync<UserUpdateEndpoint, UserUpdateRequest, ProblemDetails>(
            new() { Id = shared.Id, IsActive = false, Roles = [roleId] });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.First().Code.Should().Be(ErrorCodes.UserSharedAcrossTenants.Value);
        await AssertAliveAsync(here);
        await AssertAliveAsync(there);
    }

    /// <summary>
    /// Verifies a platform administrator changing the roles of a platform account that is a member of two
    /// tenants ends its platform-scope session and leaves its sessions in both tenants working. Nobody acting
    /// inside a tenant may make this change to a shared account - the shared-account guard refuses the
    /// tenant's administrator and a platform account inside the tenant alike - so the platform scope is the
    /// only place a shared account's roles are changed.
    /// </summary>
    [Fact]
    public async Task Changing_A_Shared_Platform_Accounts_Roles_Leaves_Its_Sessions_In_Both_Tenants_Working()
    {
        var tenantA = await CreateTenantAsync();
        var tenantB = await CreateTenantAsync();
        var shared = await CreateDualTenantMemberAsync(tenantA.Id, tenantB.Id);
        await MarkAsPlatformAccountAsync(shared.Id);
        var heldRoleId = await CreatePlatformRoleAsync();
        var newRoleId = await CreatePlatformRoleAsync();
        await UserService.AssignRoleAsync(shared.Id, heldRoleId);
        var platformSession = await SessionForAsync(shared.Username);
        var inA = await SessionForAsync(shared.Username, tenantA.Id);
        var inB = await SessionForAsync(shared.Username, tenantB.Id);

        await SetPlatformAdminAuthTokenAsync();
        var (response, _) = await Client.PUTAsync<UserUpdateEndpoint, UserUpdateRequest, UserUpdateResponse>(
            new() { Id = shared.Id, IsActive = true, Roles = [newRoleId] });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await AssertEndedAsync(platformSession);
        await AssertAliveAsync(inA);
        await AssertAliveAsync(inB);
    }

    /// <summary>
    /// Creates a tenant, an administrator of it holding exactly the user permissions these tests need, and
    /// two roles the tenant's accounts can be moved between.
    /// </summary>
    /// <returns>The tenant, a client signed in as its administrator, and the two roles.</returns>
    private async Task<(Guid TenantId, HttpClient Admin, Guid RoleId, Guid OtherRoleId)> ArrangeTenantAsync()
    {
        var tenant = await CreateTenantAsync();
        var adminRoleId = await CreateTenantRoleAsync(tenant.Id, Allow.User_Update, Allow.User_Delete, Allow.User_View);
        var administrator = await CreateTenantUserAsync(tenant.Id, adminRoleId);
        var admin = await ClientForAsync(administrator.Username, tenant.Id);
        var roleId = await CreateTenantRoleAsync(tenant.Id, Allow.User_View);
        var otherRoleId = await CreateTenantRoleAsync(tenant.Id, Allow.Role_View);

        return (tenant.Id, admin, roleId, otherRoleId);
    }

    /// <summary>
    /// Creates a platform account, a member of a tenant of its own, that the platform administrator's update
    /// does not name.
    /// </summary>
    private async Task<Backend.Features.Identity.Core.Entities.User> CreatePlatformAccountAsync()
    {
        var account = await CreateTenantUserAsync((await CreateTenantAsync()).Id);
        await MarkAsPlatformAccountAsync(account.Id);
        return account;
    }
}
