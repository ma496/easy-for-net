namespace Backend.Tests.Middleware;

using Backend.Data.Entities;
using Backend.Features.Identity.Core;
using Backend.Features.Identity.Core.Entities;
using Backend.Features.Identity.Endpoints.Account;
using Backend.Features.Identity.Endpoints.Users;
using Backend.Features.Tenancy.Endpoints.Tenants;
using Backend.Tests.Features.Tenancy;

/// <summary>
/// Tests that a session's authorization is recomputed from current data on every request, so that a
/// membership withdrawn, a tenant suspended, a switch, a role assignment replaced or a role's
/// permissions changed all take effect on the next request with the token the caller already holds -
/// no second sign-in, no password change and no waiting for the session to expire
/// (AC-020, AC-108, AC-116, AC-117, AC-127).
/// </summary>
/// <remarks>
/// <para>
/// Every test here arranges the change out of band - through the membership or role services, or
/// through the platform's own suspend surface - and then sends its next request on the very same
/// bearer token the caller was given before the change. That token is the whole point: a test that
/// signed in again would prove that a fresh session carries fresh permissions, which was never in
/// question. The refreshed token a switch issues is the one exception, and it is exactly what AC-108
/// describes: the switch establishes the selection, and what the caller may then do in the tenant it
/// selected is read from that tenant's grants at request time.
/// </para>
/// <para>
/// The probe is <see cref="UserListEndpoint"/>, which is ordinary tenant-scoped work: it is not
/// exempt from the tenant requirement, so a session whose tenant has stopped being usable is refused
/// with the reason recorded for it - a 403 carrying the tenant error code, never a 401 - while a
/// session that is simply short of the permission is refused with the framework's bare 403. Both
/// answers are asserted, because which one a caller gets is what tells them whether to choose another
/// tenant or to ask for authority.
/// </para>
/// </remarks>
public class SessionValidationMiddlewareTenantTests(App app) : TenancyTestsBase(app)
{
    /// <summary>
    /// The role service, for the two changes AC-117 is about: a permission withdrawn from a role, and
    /// the role itself deleted.
    /// </summary>
    private IRoleService RoleService => App.Services.GetRequiredService<IRoleService>();

    /// <summary>
    /// Verifies that a membership removed mid-session costs the member that tenant on their next
    /// request - with the unchanged token, without a password change and without their access to
    /// another tenant being touched (AC-020).
    /// </summary>
    [Fact]
    public async Task Membership_Revocation_Takes_Effect_On_Next_Request()
    {
        var revokedTenant = await CreateTenantAsync();
        var remainingTenant = await CreateTenantAsync();
        var revokedRoleId = await CreateTenantRoleAsync(revokedTenant.Id, Allow.User_View);
        var remainingRoleId = await CreateTenantRoleAsync(remainingTenant.Id, Allow.User_View);

        // Both tenants are given an administrator other than the member under test, so the removal is
        // never the one the last-administrator guard refuses.
        await CreateFirstMemberAsync(revokedTenant.Id);
        await CreateFirstMemberAsync(remainingTenant.Id);

        var member = await CreateTenantUserAsync(revokedTenant.Id, revokedRoleId);
        var added = await MembershipService.AddAsync(
            remainingTenant.Id, member.Id, [remainingRoleId], TestContext.Current.CancellationToken);

        added.RoleIds.Should().BeEquivalentTo([remainingRoleId],
            "the second membership grants exactly what it was asked for, which is what makes the reading of it afterwards exact");

        // The token is minted before the removal and is never replaced: everything below is sent with
        // exactly the authorization the member held when the membership still stood.
        var client = await ClientForAsync(member.Username, revokedTenant.Id);

        var (admitted, _) = await client
            .GETAsync<UserListEndpoint, UserListRequest, UserListResponse>(new());

        admitted.StatusCode.Should().Be(HttpStatusCode.OK,
            "the member holds User.View in this tenant and belongs to it");

        var passwordHash = await PasswordHashAsync(member.Id);

        await SetPlatformAdminAuthTokenAsync();

        var (removed, _) = await App.Client
            .DELETEAsync<TenantMemberRemoveEndpoint, TenantMemberRemoveRequest, TenantMemberRemoveResponse>(
                new() { TenantId = revokedTenant.Id, UserId = member.Id });

        removed.StatusCode.Should().Be(HttpStatusCode.OK);

        var (refused, refusal) = await client
            .GETAsync<UserListEndpoint, UserListRequest, ProblemDetails>(new());

        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "the membership that admitted the member is gone, and the session check found that out on this very request");
        refused.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized,
            "a revoked membership ends no session: the caller is told which tenant they lost, not asked who they are");

        refusal.Errors.Should().ContainSingle();
        refusal.Errors.First().Code.Should().Be(ErrorCodes.TenantMembershipRevoked);

        (await PasswordHashAsync(member.Id)).Should().Be(passwordHash,
            "nothing about the account was changed to bring this about - the same credentials sign the same account in");

        // The other tenant is untouched: the account still belongs to it, still holds its role, and
        // still reaches it by the ordinary way of selecting it.
        await TestsHelper.SwitchTenantAsync(client, remainingTenant.Id);

        var (elsewhere, _) = await client
            .GETAsync<UserListEndpoint, UserListRequest, UserListResponse>(new());

        elsewhere.StatusCode.Should().Be(HttpStatusCode.OK,
            "losing one tenant costs the member that tenant alone");
    }

    /// <summary>
    /// Verifies that switching the active tenant determines the caller's permissions from the
    /// membership and roles of the tenant being switched into, as they stand at request time, rather
    /// than carrying across the authority established for the tenant left behind (AC-108).
    /// </summary>
    [Fact]
    public async Task Switch_Recomputes_Permissions_At_Request_Time()
    {
        var limitedTenant = await CreateTenantAsync();
        var administeredTenant = await CreateTenantAsync();

        await CreateFirstMemberAsync(limitedTenant.Id);
        await CreateFirstMemberAsync(administeredTenant.Id);

        // One account, limited in one tenant and an administrator in the other - the shape that makes
        // the authority of the two tenants distinguishable on the same call.
        var account = await CreateTenantUserAsync(
            limitedTenant.Id, await CreateTenantRoleAsync(limitedTenant.Id, Allow.Tenant_View));
        var administratorRoleId = await TenantAdministratorRoleIdAsync(administeredTenant.Id);

        var added = await MembershipService.AddAsync(
            administeredTenant.Id, account.Id, [administratorRoleId], TestContext.Current.CancellationToken);

        added.RoleIds.Should().BeEquivalentTo([administratorRoleId]);

        var client = await ClientForAsync(account.Username, limitedTenant.Id);

        var (refused, _) = await client
            .GETAsync<UserListEndpoint, UserListRequest, UserListResponse>(new());

        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "in this tenant the account holds Tenant.View and nothing else, so reading accounts is beyond it");

        await TestsHelper.SwitchTenantAsync(client, administeredTenant.Id);

        var (admitted, _) = await client
            .GETAsync<UserListEndpoint, UserListRequest, UserListResponse>(new());

        admitted.StatusCode.Should().Be(HttpStatusCode.OK,
            "the tenant just selected administers its own accounts, and the authority reported is that tenant's - read now, not carried over from the tenant left behind");
    }

    /// <summary>
    /// Verifies that replacing a member's role assignments in a tenant applies to their existing
    /// session from the next request, without a sign-in and without waiting for the session to expire
    /// (AC-116).
    /// </summary>
    [Fact]
    public async Task Role_Assignment_Change_Applies_On_Next_Request()
    {
        var tenant = await CreateTenantAsync();
        await CreateFirstMemberAsync(tenant.Id);

        var roleId = await CreateTenantRoleAsync(tenant.Id, Allow.User_View);
        var member = await CreateTenantUserAsync(tenant.Id, roleId);
        var client = await ClientForAsync(member.Username, tenant.Id);

        var (admitted, _) = await client
            .GETAsync<UserListEndpoint, UserListRequest, UserListResponse>(new());

        admitted.StatusCode.Should().Be(HttpStatusCode.OK, "the member holds the role that grants User.View");

        await TenantAuthorizationService.ReplaceTenantRoleAssignmentsAsync(
            tenant.Id, member.Id, [], TestContext.Current.CancellationToken);

        var (refused, _) = await client
            .GETAsync<UserListEndpoint, UserListRequest, UserListResponse>(new());

        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "the assignment that granted the permission is gone, and the token the member already held was enough to find that out");

        // The membership itself is untouched: what changed is what it grants, not who belongs.
        (await MembershipService.IsMemberAsync(tenant.Id, member.Id, TestContext.Current.CancellationToken))
            .Should().BeTrue("replacing a member's roles withdraws authority, not membership");
        (await GrantedRoleNamesAsync(tenant.Id, member.Id)).Should().BeEmpty();
    }

    /// <summary>
    /// Verifies that changing a tenant role's permissions, and deleting the role outright, apply to
    /// the existing sessions of every member holding it from their next request - in both cases on the
    /// unchanged token (AC-117).
    /// </summary>
    [Fact]
    public async Task Role_Permission_Change_Applies_On_Next_Request()
    {
        var permissionId = await PermissionIdAsync(Allow.User_View);

        // The permission withdrawn from the role.
        var changedTenant = await CreateTenantAsync();
        await CreateFirstMemberAsync(changedTenant.Id);

        var changedRoleId = await CreateTenantRoleAsync(changedTenant.Id, Allow.User_View);
        var changedHolder = await CreateTenantUserAsync(changedTenant.Id, changedRoleId);
        var changedClient = await ClientForAsync(changedHolder.Username, changedTenant.Id);

        var (admittedBefore, _) = await changedClient
            .GETAsync<UserListEndpoint, UserListRequest, UserListResponse>(new());

        admittedBefore.StatusCode.Should().Be(HttpStatusCode.OK);

        await TenantScopedAsync(changedTenant.Id, () => RoleService.RemovePermissionAsync(changedRoleId, permissionId));

        var (refusedAfter, _) = await changedClient
            .GETAsync<UserListEndpoint, UserListRequest, UserListResponse>(new());

        refusedAfter.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "the role still stands and is still assigned, and it no longer grants what the call needs");

        // The role deleted outright.
        var deletedTenant = await CreateTenantAsync();
        await CreateFirstMemberAsync(deletedTenant.Id);

        var deletedRoleId = await CreateTenantRoleAsync(deletedTenant.Id, Allow.User_View);
        var deletedHolder = await CreateTenantUserAsync(deletedTenant.Id, deletedRoleId);
        var deletedClient = await ClientForAsync(deletedHolder.Username, deletedTenant.Id);

        var (admittedWhileItStood, _) = await deletedClient
            .GETAsync<UserListEndpoint, UserListRequest, UserListResponse>(new());

        admittedWhileItStood.StatusCode.Should().Be(HttpStatusCode.OK);

        await TenantScopedAsync(deletedTenant.Id, () => RoleService.DeleteAsync(deletedRoleId));

        var (refusedOnceDeleted, _) = await deletedClient
            .GETAsync<UserListEndpoint, UserListRequest, UserListResponse>(new());

        refusedOnceDeleted.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "a deleted role grants nothing, and the session is rebuilt from the roles that are still there");
    }

    /// <summary>
    /// Verifies that a stored selection naming a tenant that has since been suspended is discarded
    /// rather than honoured: the next tenant-scoped request is refused with that reason, and the caller
    /// is told by the identity call that they are acting in no tenant and must choose again (AC-127).
    /// </summary>
    [Fact]
    public async Task Stale_Selection_Is_Not_Honoured()
    {
        var tenant = await CreateTenantAsync();
        await CreateFirstMemberAsync(tenant.Id);

        var member = await CreateTenantUserAsync(tenant.Id, await CreateTenantRoleAsync(tenant.Id, Allow.User_View));
        var client = await ClientForAsync(member.Username, tenant.Id);

        var (admitted, _) = await client
            .GETAsync<UserListEndpoint, UserListRequest, UserListResponse>(new());

        admitted.StatusCode.Should().Be(HttpStatusCode.OK, "the tenant is in service and the member holds the permission in it");

        // Suspended through the platform's own surface, so the tenant is in exactly the state a real
        // suspension leaves it in - and the member's token is left untouched throughout.
        await SetPlatformAdminAuthTokenAsync();

        var (suspended, _) = await App.Client
            .POSTAsync<TenantSuspendEndpoint, TenantSuspendRequest, TenantSuspendResponse>(new() { Id = tenant.Id });

        suspended.StatusCode.Should().Be(HttpStatusCode.OK);

        var (refused, refusal) = await client
            .GETAsync<UserListEndpoint, UserListRequest, ProblemDetails>(new());

        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "the selection still names the suspended tenant, and a tenant that is out of service is not one to keep sending");
        refused.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized, "suspension is not a sign-out");

        refusal.Errors.Should().ContainSingle();
        refusal.Errors.First().Code.Should().Be(ErrorCodes.TenantSuspended);

        var (infoResponse, info) = await client
            .GETAsync<GetInfoEndpoint, UserGetInfoResponse>();

        infoResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "the caller is still who they were and can still ask who they are");
        info.ActiveTenantId.Should().BeNull(
            "the stale selection is discarded rather than reported back as the tenant being acted in");
        info.ActiveTenant.Should().BeNull();
        info.Tenants.Should().NotContain(candidate => candidate.Id == tenant.Id,
            "nor is a tenant that cannot be selected offered as a choice");
    }

    /// <summary>
    /// Creates a tenant whose first member is not the account under test, so that a later membership
    /// change is never the one the last-administrator rule refuses.
    /// </summary>
    /// <param name="tenantId">The tenant to give an administrator.</param>
    private async Task CreateFirstMemberAsync(Guid tenantId)
        => await CreateTenantUserAsync(tenantId, await TenantAdministratorRoleIdAsync(tenantId));

    /// <summary>
    /// The identity of the role a tenant was provisioned with - the one holding tenant administration -
    /// read from the tenant's own system-created role rather than from a name.
    /// </summary>
    /// <param name="tenantId">The tenant whose administrator role is read.</param>
    /// <returns>The identifier of that role.</returns>
    private async Task<Guid> TenantAdministratorRoleIdAsync(Guid tenantId)
        => await DbContext.Roles
            .AcrossAllTenants()
            .AsNoTracking()
            .Where(role => role.TenantId == tenantId && role.SystemCreated)
            .Select(role => role.Id)
            .SingleAsync(TestContext.Current.CancellationToken);

    /// <summary>
    /// The names of the roles an account holds in one tenant right now, read from the assignments so
    /// that what a session is rebuilt from is what the database says stands.
    /// </summary>
    /// <param name="tenantId">The tenant the roles are read in.</param>
    /// <param name="userId">The account whose roles are read.</param>
    /// <returns>The names of the roles it holds there.</returns>
    private async Task<List<string>> GrantedRoleNamesAsync(Guid tenantId, Guid userId)
        => await DbContext.Roles
            .AcrossAllTenants()
            .AsNoTracking()
            .Where(role => role.TenantId == tenantId
                           && DbContext.UserRoles.Any(assignment => assignment.UserId == userId && assignment.RoleId == role.Id))
            .Select(role => role.Name)
            .ToListAsync(TestContext.Current.CancellationToken);

    /// <summary>
    /// The account's stored password hash, read so that a test can show that access was withdrawn
    /// without the account's credentials being touched.
    /// </summary>
    /// <param name="userId">The account to read.</param>
    /// <returns>The stored hash.</returns>
    private async Task<string> PasswordHashAsync(Guid userId)
        => await DbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => user.PasswordHash)
            .SingleAsync(TestContext.Current.CancellationToken);

    /// <summary>
    /// The identity of a permission by its name, for a test that changes what a role grants rather
    /// than which role an account holds.
    /// </summary>
    /// <param name="name">The permission's name.</param>
    /// <returns>The identifier of that permission.</returns>
    private async Task<Guid> PermissionIdAsync(string name)
        => await DbContext.Permissions
            .AsNoTracking()
            .Where(permission => permission.Name == name)
            .Select(permission => permission.Id)
            .SingleAsync(TestContext.Current.CancellationToken);
}
