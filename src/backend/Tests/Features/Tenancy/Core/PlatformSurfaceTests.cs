namespace Backend.Tests.Features.Tenancy.Core;

using Backend.ShareData.Entities;
using Backend.Features.Identity.Core.Entities;
using Backend.Features.Identity.Endpoints.Roles;
using Backend.Features.Tenancy.Endpoints.Tenants;

/// <summary>
/// Tests for what platform administration reaches without a membership - the whole point of it being a
/// scope of its own rather than a role inside a tenant.
/// </summary>
/// <remarks>
/// A tenant's own administrator administers one tenant, and administering a tenant is never a way of
/// administering the platform. The converse is what is asserted here: a platform administrator acts on a
/// tenant they have never joined, and the evidence is that they can - the membership they add is the
/// proof, because it is a row that could only have been written by somebody entitled to write it.
/// </remarks>
public class PlatformSurfaceTests(App app) : TenancyTestsBase(app)
{
    /// <summary>
    /// Verifies that a platform administrator administers a tenant they hold no membership in: the member
    /// they add joins the tenant, without the administrator joining it in the process.
    /// </summary>
    [Fact]
    public async Task Platform_Administrator_Administers_A_Tenant_It_Is_Not_A_Member_Of()
    {
        // Created without a first member and read by nobody but this test, so the only standing anybody
        // has in it is what is arranged below.
        var tenant = await CreateTenantAsync();
        var member = await CreateAccountWithoutMembershipAsync();
        var roleId = await CreateTenantRoleAsync(tenant.Id, Allow.Role_View);
        var administrator = await ReadSeededAdministratorAsync();

        // The seeded platform administrator, signed in as itself: it holds the platform role and no
        // membership at all, so it is not a member of the tenant below.
        await SetPlatformAdminAuthTokenAsync();

        var (addResponse, added) = await Client
            .POSTAsync<TenantMemberAddEndpoint, TenantMemberAddRequest, TenantMemberAddResponse>(new()
            {
                TenantId = tenant.Id,
                UserId = member.Id,
                Roles = [roleId]
            });

        addResponse.StatusCode.Should().Be(
            HttpStatusCode.OK,
            "the route names the tenant being administered, so it is reachable without the caller working in it");
        added.TenantId.Should().Be(tenant.Id);
        added.UserId.Should().Be(member.Id);
        added.Roles.Should().Contain(roleId, "the member holds the role the request asked for");

        var membership = await DbContext.TenantMemberships
            .AcrossAllTenants()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.TenantId == tenant.Id && candidate.UserId == member.Id,
                TestContext.Current.CancellationToken);

        membership.Should().NotBeNull("the membership row appears, written by a caller who is not a member of the tenant");
        membership!.IsDeleted.Should().BeFalse();

        var administratorsOwnMembership = await DbContext.TenantMemberships
            .AcrossAllTenants()
            .AsNoTracking()
            .CountAsync(
                candidate => candidate.TenantId == tenant.Id && candidate.UserId == administrator.Id,
                TestContext.Current.CancellationToken);

        administratorsOwnMembership.Should().Be(
            0,
            "administering a tenant is not joining it: the platform administrator's own memberships are unchanged");
    }

    /// <summary>
    /// Verifies that a platform administrator can read a tenant's members without belonging to it, so the
    /// surface is usable for administration rather than only for the one write above.
    /// </summary>
    [Fact]
    public async Task Platform_Administrator_Lists_The_Members_Of_A_Tenant_It_Is_Not_A_Member_Of()
    {
        var tenant = await CreateTenantAsync();
        var member = await CreateAccountWithoutMembershipAsync();
        var roleId = await CreateTenantRoleAsync(tenant.Id, Allow.Role_View);

        await SetPlatformAdminAuthTokenAsync();

        var (addResponse, _) = await Client
            .POSTAsync<TenantMemberAddEndpoint, TenantMemberAddRequest, TenantMemberAddResponse>(new()
            {
                TenantId = tenant.Id,
                UserId = member.Id,
                Roles = [roleId]
            });

        addResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var (listResponse, page) = await Client
            .GETAsync<TenantMemberListEndpoint, TenantMemberListRequest, TenantMemberListResponse>(
                new() { TenantId = tenant.Id, All = true });

        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        page.Items.Select(item => item.Id).Should().Equal(
            [member.Id],
            "the tenant's members are listed for a caller with no membership in it, and the row identity is the account's");
    }

    /// <summary>
    /// Verifies that the platform's own surface is out of reach of a caller acting inside a tenant,
    /// however the roles they hold were granted: the permission it declares is platform-scoped, and a
    /// session acting in a tenant never carries one.
    /// </summary>
    /// <remarks>
    /// The account is deliberately given the platform administrator role, so what refuses it is the
    /// scope its session acts in rather than a grant it was never given. That narrowing is the whole of
    /// the rule keeping the platform surface out of a tenant's reach - there is no second mechanism
    /// beside it.
    /// </remarks>
    [Fact]
    public async Task Platform_Surface_Is_Out_Of_Reach_From_Inside_A_Tenant()
    {
        var tenant = await CreateTenantAsync();
        var account = await CreateTenantUserAsync(tenant.Id);

        await UserService.AssignRoleAsync(account.Id, TestRoles.PlatformAdminRoleId);

        var client = await ClientForAsync(account.Username, tenant.Id);

        var (response, refusal) = await client
            .GETAsync<TenantListEndpoint, TenantListRequest, ProblemDetails>(new());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        refusal.Errors.Should().ContainSingle();
        refusal.Errors.First().Code.Should().Be(ErrorCodes.PermissionDenied,
            "listing every tenant is platform-scoped, and a session acting inside a tenant holds no platform-scoped permission");
    }

    /// <summary>
    /// Reads the account the seeder creates, which is the platform administrator these tests act as.
    /// </summary>
    /// <returns>The seeded administrator account.</returns>
    private async Task<User> ReadSeededAdministratorAsync()
        => await DbContext.Users
            .AsNoTracking()
            .SingleAsync(user => user.UsernameNormalized == TestUsers.PlatformAdminUsername, TestContext.Current.CancellationToken);
}