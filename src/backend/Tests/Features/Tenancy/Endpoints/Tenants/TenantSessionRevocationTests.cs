namespace Backend.Tests.Features.Tenancy.Endpoints.Tenants;

using Backend.ShareData.Entities;
using Backend.Features.Identity.Core.Entities;
using Backend.Features.Tenancy.Core;
using Backend.Features.Tenancy.Core.Entities;
using Backend.Features.Tenancy.Endpoints.Tenants;
using Backend.Tests.Features.Identity;

/// <summary>
/// Tests for the sessions a tenant administration change ends: removing a member, replacing a member's
/// roles, suspending a tenant and deleting one. Each change ends exactly the sessions it names - the
/// access token answers 401 with no renewal and the refresh token is refused - and leaves every other
/// session working, which is what makes revoking on the change safe rather than blunt.
/// </summary>
/// <remarks>
/// Every tenant, role and account is made by the test itself, and the change is made through the
/// endpoint by a platform administrator, so no seeded tenant, role or account is ever changed.
/// </remarks>
public class TenantSessionRevocationTests(App app) : SessionRevocationTestsBase(app)
{
    /// <summary>
    /// Verifies that removing a member from one tenant ends their session in it and leaves their session
    /// in another tenant, and another member's session in the same tenant, working.
    /// </summary>
    [Fact]
    public async Task Member_Removal_Ends_Only_The_Removed_Members_Session_In_That_Tenant()
    {
        var tenantA = await CreateTenantAsync();
        var tenantB = await CreateTenantAsync();
        await CreateFirstMemberAsync(tenantA.Id);
        await CreateFirstMemberAsync(tenantB.Id);
        var member = await CreateDualTenantMemberAsync(tenantA.Id, tenantB.Id);
        var bystander = await CreateTenantUserAsync(tenantA.Id);

        var memberInA = await SessionForAsync(member.Username, tenantA.Id);
        var memberInB = await SessionForAsync(member.Username, tenantB.Id);
        var bystanderInA = await SessionForAsync(bystander.Username, tenantA.Id);

        await SetPlatformAdminAuthTokenAsync();
        var (response, _) = await Client
            .DELETEAsync<TenantMemberRemoveEndpoint, TenantMemberRemoveRequest, TenantMemberRemoveResponse>(
                new() { TenantId = tenantA.Id, UserId = member.Id });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await AssertEndedAsync(memberInA);
        await AssertAliveAsync(memberInB);
        await AssertAliveAsync(bystanderInA);
    }

    /// <summary>
    /// Verifies that a removal the endpoint refuses because it would leave the tenant with no
    /// administrator ends no session.
    /// </summary>
    [Fact]
    public async Task Refused_Member_Removal_Ends_No_Session()
    {
        var tenant = await CreateTenantAsync();
        var administrator = await CreateFirstMemberAsync(tenant.Id);
        var administratorSession = await SessionForAsync(administrator.Username, tenant.Id);

        await SetPlatformAdminAuthTokenAsync();
        var (response, problem) = await Client
            .DELETEAsync<TenantMemberRemoveEndpoint, TenantMemberRemoveRequest, ProblemDetails>(
                new() { TenantId = tenant.Id, UserId = administrator.Id });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.First().Code.Should().Be(ErrorCodes.LastTenantAdministrator.Value);

        await AssertAliveAsync(administratorSession);
    }

    /// <summary>
    /// Verifies that replacing a member's roles in one tenant ends their session in it and leaves their
    /// session in another tenant working.
    /// </summary>
    [Fact]
    public async Task Role_Replacement_Ends_Only_The_Members_Session_In_That_Tenant()
    {
        var tenantA = await CreateTenantAsync();
        var tenantB = await CreateTenantAsync();
        await CreateFirstMemberAsync(tenantA.Id);
        await CreateFirstMemberAsync(tenantB.Id);
        var roleInA = await CreateTenantRoleAsync(tenantA.Id, Allow.User_View);
        var member = await CreateDualTenantMemberAsync(tenantA.Id, tenantB.Id);

        var memberInA = await SessionForAsync(member.Username, tenantA.Id);
        var memberInB = await SessionForAsync(member.Username, tenantB.Id);

        await SetPlatformAdminAuthTokenAsync();
        var (response, _) = await Client
            .PUTAsync<TenantMemberUpdateRolesEndpoint, TenantMemberUpdateRolesRequest, TenantMemberUpdateRolesResponse>(
                new() { TenantId = tenantA.Id, UserId = member.Id, Roles = [roleInA] });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await AssertEndedAsync(memberInA);
        await AssertAliveAsync(memberInB);
    }

    /// <summary>
    /// Verifies that a replacement the endpoint refuses because it would withdraw the tenant's last
    /// administration ends no session.
    /// </summary>
    [Fact]
    public async Task Refused_Role_Replacement_Ends_No_Session()
    {
        var tenant = await CreateTenantAsync();
        var administrator = await CreateFirstMemberAsync(tenant.Id);
        var administratorSession = await SessionForAsync(administrator.Username, tenant.Id);

        await SetPlatformAdminAuthTokenAsync();
        var (response, problem) = await Client
            .PUTAsync<TenantMemberUpdateRolesEndpoint, TenantMemberUpdateRolesRequest, ProblemDetails>(
                new() { TenantId = tenant.Id, UserId = administrator.Id, Roles = [] });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.First().Code.Should().Be(ErrorCodes.LastTenantAdministrator.Value);

        await AssertAliveAsync(administratorSession);
    }

    /// <summary>
    /// Verifies that suspending a tenant ends every session acting in it, and leaves another tenant's
    /// sessions, the same account's session in that other tenant, and a platform-scope session working.
    /// </summary>
    [Fact]
    public async Task Suspension_Ends_Every_Session_In_The_Tenant_And_No_Other()
    {
        var (suspended, _, sessionsInSuspended, sessionsElsewhere) = await ArrangeTwoTenantsAsync();

        await SetPlatformAdminAuthTokenAsync();
        var (response, _) = await Client
            .POSTAsync<TenantSuspendEndpoint, TenantSuspendRequest, TenantSuspendResponse>(new() { Id = suspended.Id });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        foreach (var session in sessionsInSuspended)
        {
            await AssertEndedAsync(session);
        }

        foreach (var session in sessionsElsewhere)
        {
            await AssertAliveAsync(session);
        }
    }

    /// <summary>
    /// Verifies that deleting a tenant ends every session acting in it, and leaves another tenant's
    /// sessions, the same account's session in that other tenant, and a platform-scope session working.
    /// </summary>
    [Fact]
    public async Task Deletion_Ends_Every_Session_In_The_Tenant_And_No_Other()
    {
        var (deleted, _, sessionsInDeleted, sessionsElsewhere) = await ArrangeTwoTenantsAsync();

        await SetPlatformAdminAuthTokenAsync();
        var (response, _) = await Client
            .DELETEAsync<TenantDeleteEndpoint, TenantDeleteRequest, TenantDeleteResponse>(new() { Id = deleted.Id });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        foreach (var session in sessionsInDeleted)
        {
            await AssertEndedAsync(session);
        }

        foreach (var session in sessionsElsewhere)
        {
            await AssertAliveAsync(session);
        }
    }

    /// <summary>
    /// Verifies that a suspension the endpoint refuses because the tenant is the system-created one
    /// ends no session. The bootstrap tenant is only read and never changed.
    /// </summary>
    [Fact]
    public async Task Refused_Suspension_Ends_No_Session()
    {
        var bootstrap = await CreateTenantSessionOfBootstrapTenantAsync();

        await SetPlatformAdminAuthTokenAsync();
        var (response, problem) = await Client
            .POSTAsync<TenantSuspendEndpoint, TenantSuspendRequest, ProblemDetails>(new() { Id = TestTenants.BootstrapTenantId });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.First().Code.Should().Be(ErrorCodes.SystemCreatedTenantCannotBeModified.Value);

        await AssertAliveAsync(bootstrap);
    }

    /// <summary>
    /// A session of a freshly made account in the seeded bootstrap tenant, so that the refused change
    /// has a live session to leave alone without touching a seeded account.
    /// </summary>
    private async Task<RenewableSession> CreateTenantSessionOfBootstrapTenantAsync()
    {
        var account = await CreateTenantUserAsync(TestTenants.BootstrapTenantId);
        return await SessionForAsync(account.Username, TestTenants.BootstrapTenantId);
    }

    /// <summary>
    /// Makes two tenants, two members of the first, one member of the second, one account that belongs
    /// to both and a platform account acting in no tenant, and signs each of them in.
    /// </summary>
    /// <returns>
    /// The first tenant, the second, the sessions acting in the first, and the sessions that must survive
    /// a change to the first: the second tenant's, the dual member's session there and the platform's.
    /// </returns>
    private async Task<(Tenant First, Tenant Second, List<RenewableSession> InFirst, List<RenewableSession> Elsewhere)> ArrangeTwoTenantsAsync()
    {
        var first = await CreateTenantAsync();
        var second = await CreateTenantAsync();
        var firstAdministrator = await CreateFirstMemberAsync(first.Id);
        var firstMember = await CreateTenantUserAsync(first.Id);
        var secondMember = await CreateTenantUserAsync(second.Id);
        var dual = await CreateDualTenantMemberAsync(first.Id, second.Id);

        var platformAccount = await CreateAccountWithoutMembershipAsync();
        await MarkAsPlatformAccountAsync(platformAccount.Id);

        var inFirst = new List<RenewableSession>
        {
            await SessionForAsync(firstAdministrator.Username, first.Id),
            await SessionForAsync(firstMember.Username, first.Id),
            await SessionForAsync(dual.Username, first.Id)
        };
        var elsewhere = new List<RenewableSession>
        {
            await SessionForAsync(secondMember.Username, second.Id),
            await SessionForAsync(dual.Username, second.Id),
            await SessionForAsync(platformAccount.Username)
        };

        return (first, second, inFirst, elsewhere);
    }

    /// <summary>
    /// Gives a tenant its first member, who is granted the tenant's administrator role, so that a
    /// member removed or re-roled afterwards is not the tenant's only administrator.
    /// </summary>
    /// <param name="tenantId">The tenant to give a first member to.</param>
    /// <returns>The account that became the tenant's first member.</returns>
    private async Task<User> CreateFirstMemberAsync(Guid tenantId)
    {
        var firstMember = await CreateAccountWithoutMembershipAsync();

        var result = await MembershipService.AddAsync(tenantId, firstMember.Id, [], TestContext.Current.CancellationToken);

        result.RoleIds.Should().ContainSingle("a tenant's first member is granted its administrator role");
        return firstMember;
    }
}
