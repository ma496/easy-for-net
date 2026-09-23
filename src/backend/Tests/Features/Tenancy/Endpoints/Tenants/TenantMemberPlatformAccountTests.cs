namespace Backend.Tests.Features.Tenancy.Endpoints.Tenants;

using Backend.Features.Identity.Core.Entities;
using Backend.Features.Identity.Endpoints.Roles;
using Backend.Features.Tenancy.Core.Entities;
using Backend.Features.Tenancy.Endpoints.Tenants;

/// <summary>
/// Tests that a platform account is administered from platform scope only: a tenant's own
/// administrators neither see it among the tenant's members nor add, re-role or remove it, and the
/// role user counts they are shown leave it out - while a platform account acting in no tenant sees
/// and counts it.
/// </summary>
public class TenantMemberPlatformAccountTests(App app) : TenancyTestsBase(app)
{
    /// <summary>
    /// Verifies that the tenant's member list and the role list's user count both leave a platform
    /// member out for the tenant's own administrator.
    /// </summary>
    [Fact]
    public async Task Tenant_Administrator_Does_Not_See_Or_Count_A_Platform_Member()
    {
        var (tenant, administrator, _, administratorRoleId) = await CreateTenantWithPlatformMemberAsync();
        var client = await ClientForAsync(administrator.Username, tenant.Id);

        var (listRsp, list) = await client
            .GETAsync<TenantMemberListEndpoint, TenantMemberListRequest, TenantMemberListResponse>(
                new() { TenantId = tenant.Id, Page = 1, PageSize = 100 });

        listRsp.StatusCode.Should().Be(HttpStatusCode.OK);
        list.Items.Select(item => item.Id).Should().Equal([administrator.Id],
            "a platform account is administered from platform scope only, so its membership does not put it on the tenant's list");
        list.Total.Should().Be(1);

        var (rolesRsp, roles) = await client
            .GETAsync<RoleListEndpoint, RoleListRequest, RoleListResponse>(new() { All = true });

        rolesRsp.StatusCode.Should().Be(HttpStatusCode.OK);
        roles.Items.Single(item => item.Id == administratorRoleId).UserCount.Should().Be(1,
            "the platform member holds the role too, but it is nobody the tenant administers");
    }

    /// <summary>
    /// Verifies that the tenant's own administrator cannot re-role or remove a platform member: both
    /// read it as a missing membership, and neither writes anything.
    /// </summary>
    [Fact]
    public async Task Tenant_Administrator_Cannot_Change_A_Platform_Member()
    {
        var (tenant, administrator, platformMember, _) = await CreateTenantWithPlatformMemberAsync();
        var client = await ClientForAsync(administrator.Username, tenant.Id);

        var updateRsp = await client
            .PUTAsync<TenantMemberUpdateRolesEndpoint, TenantMemberUpdateRolesRequest, EmptyResponse>(
                new() { TenantId = tenant.Id, UserId = platformMember.Id, Roles = [] });

        updateRsp.Response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var removeRsp = await client
            .DELETEAsync<TenantMemberRemoveEndpoint, TenantMemberRemoveRequest, EmptyResponse>(
                new() { TenantId = tenant.Id, UserId = platformMember.Id });

        removeRsp.Response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await MembershipService.IsMemberAsync(tenant.Id, platformMember.Id, TestContext.Current.CancellationToken))
            .Should().BeTrue("a refused removal leaves the membership in place");
    }

    /// <summary>
    /// Verifies that the tenant's own administrator cannot bring a platform account into the tenant:
    /// to them it reads exactly as an account that does not exist.
    /// </summary>
    [Fact]
    public async Task Tenant_Administrator_Cannot_Add_A_Platform_Account()
    {
        var (tenant, administrator, _, _) = await CreateTenantWithPlatformMemberAsync();
        var outsider = await CreateAccountWithoutMembershipAsync();
        await MarkAsPlatformAccountAsync(outsider.Id);
        var client = await ClientForAsync(administrator.Username, tenant.Id);

        var (response, problem) = await client
            .POSTAsync<TenantMemberAddEndpoint, TenantMemberAddRequest, ProblemDetails>(
                new() { TenantId = tenant.Id, UserId = outsider.Id, Roles = [] });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.Should().ContainSingle();
        problem.Errors.First().Code.Should().Be(ErrorCodes.UserNotFound);
        (await MembershipService.IsMemberAsync(tenant.Id, outsider.Id, TestContext.Current.CancellationToken))
            .Should().BeFalse();
    }

    /// <summary>
    /// Verifies the other half of the rule: administered from platform scope, the tenant's member list
    /// and its roles' user counts include the platform member.
    /// </summary>
    [Fact]
    public async Task Platform_Administration_Sees_And_Counts_A_Platform_Member()
    {
        var (tenant, administrator, platformMember, administratorRoleId) = await CreateTenantWithPlatformMemberAsync();
        await SetPlatformAdminAuthTokenAsync();

        var (listRsp, list) = await Client
            .GETAsync<TenantMemberListEndpoint, TenantMemberListRequest, TenantMemberListResponse>(
                new() { TenantId = tenant.Id, Page = 1, PageSize = 100 });

        listRsp.StatusCode.Should().Be(HttpStatusCode.OK);
        list.Items.Select(item => item.Id).Should().BeEquivalentTo([administrator.Id, platformMember.Id]);

        var (rolesRsp, roles) = await Client
            .GETAsync<RoleListEndpoint, RoleListRequest, RoleListResponse>(new() { TenantId = tenant.Id, All = true });

        rolesRsp.StatusCode.Should().Be(HttpStatusCode.OK);
        roles.Items.Single(item => item.Id == administratorRoleId).UserCount.Should().Be(2);
    }

    /// <summary>
    /// Creates a tenant administered by an ordinary account - its first member, which is granted the
    /// tenant's administrator role - and joined by a platform account holding that same role there.
    /// </summary>
    /// <returns>The tenant, its administrator, the platform member and the administrator role.</returns>
    private async Task<(Tenant Tenant, User Administrator, User PlatformMember, Guid AdministratorRoleId)> CreateTenantWithPlatformMemberAsync()
    {
        var tenant = await CreateTenantAsync();

        var administrator = await CreateAccountWithoutMembershipAsync();
        var added = await MembershipService.AddAsync(tenant.Id, administrator.Id, [], TestContext.Current.CancellationToken);
        var administratorRoleId = added.RoleIds.Single();

        var platformMember = await CreateTenantUserAsync(tenant.Id, administratorRoleId);
        await MarkAsPlatformAccountAsync(platformMember.Id);

        return (tenant, administrator, platformMember, administratorRoleId);
    }
}
