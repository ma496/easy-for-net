namespace Backend.Tests.Features.Tenancy.Endpoints.Tenants;

using Backend.Features.Identity.Core.Entities;
using Backend.Features.Tenancy.Endpoints.Tenants;

/// <summary>
/// Tests that the four membership surfaces authorize the tenant named in their route rather than the
/// tenant the caller's session happens to be acting in.
/// </summary>
/// <remarks>
/// <para>
/// A request's permission claims are minted for one tenant: the one its session acts in. These four
/// endpoints, however, take the tenant they administer from the route, so the claims describe a
/// different tenant from the one being changed whenever the two disagree. Standing therefore has to
/// be read for the tenant in the route, and the account that can be an administrator of one tenant
/// and an ordinary member of the next is exactly the case that shows why.
/// </para>
/// <para>
/// Every test below arranges that account: the caller administers a tenant of their own - which is
/// what puts the membership permissions into the claims their request carries - and holds a bare
/// membership, with no role at all, of the tenant they address. Anyone can reach that standing
/// unaided, by onboarding a tenant of their own, so a membership surface that admitted it would let
/// any member of a tenant take that tenant over.
/// </para>
/// </remarks>
public class TenantMemberCrossTenantAuthorizationTests(App app) : TenancyTestsBase(app)
{
    /// <summary>
    /// Verifies that a caller administering another tenant cannot grant themselves the addressed
    /// tenant's administrator role, and that the request writes nothing.
    /// </summary>
    [Fact]
    public async Task Administration_Of_Another_Tenant_Does_Not_Replace_Roles_Here()
    {
        var addressed = await CreateTenantAsync();
        await CreateFirstMemberAsync(addressed.Id);
        var administratorRoleId = await TenantAdministratorRoleIdAsync(addressed.Id);
        var (caller, callerClient) = await CreateMemberAdministeringAnotherTenantAsync(addressed.Id);

        var (response, problem) = await callerClient
            .PUTAsync<TenantMemberUpdateRolesEndpoint, TenantMemberUpdateRolesRequest, ProblemDetails>(
                new() { TenantId = addressed.Id, UserId = caller.Id, Roles = [administratorRoleId] });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.Should().ContainSingle();
        problem.Errors.First().Code.Should().Be(ErrorCodes.NotTenantMember);

        (await GrantedRolesAsync(addressed.Id, caller.Id)).Should().BeEmpty(
            "administering one tenant confers nothing in another, so the caller holds in the tenant they addressed exactly what they held before: nothing");
    }

    /// <summary>
    /// Verifies that such a caller cannot put another account into the tenant they address.
    /// </summary>
    [Fact]
    public async Task Administration_Of_Another_Tenant_Does_Not_Add_A_Member_Here()
    {
        var addressed = await CreateTenantAsync();
        await CreateFirstMemberAsync(addressed.Id);
        var (_, callerClient) = await CreateMemberAdministeringAnotherTenantAsync(addressed.Id);
        var outsider = await CreateAccountWithoutMembershipAsync();

        var (response, problem) = await callerClient
            .POSTAsync<TenantMemberAddEndpoint, TenantMemberAddRequest, ProblemDetails>(
                new() { TenantId = addressed.Id, UserId = outsider.Id, Roles = [] });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.Should().ContainSingle();
        problem.Errors.First().Code.Should().Be(ErrorCodes.NotTenantMember);

        (await MembershipService.IsMemberAsync(addressed.Id, outsider.Id, TestContext.Current.CancellationToken))
            .Should().BeFalse("the refusal happens before anything is written");
    }

    /// <summary>
    /// Verifies that such a caller cannot remove the tenant's own administrator, which would leave the
    /// tenant to whoever remained.
    /// </summary>
    [Fact]
    public async Task Administration_Of_Another_Tenant_Does_Not_Remove_A_Member_Here()
    {
        var addressed = await CreateTenantAsync();
        var administrator = await CreateFirstMemberAsync(addressed.Id);
        var (_, callerClient) = await CreateMemberAdministeringAnotherTenantAsync(addressed.Id);

        var (response, problem) = await callerClient
            .DELETEAsync<TenantMemberRemoveEndpoint, TenantMemberRemoveRequest, ProblemDetails>(
                new() { TenantId = addressed.Id, UserId = administrator.Id });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.Should().ContainSingle();
        problem.Errors.First().Code.Should().Be(ErrorCodes.NotTenantMember);

        (await MembershipService.IsMemberAsync(addressed.Id, administrator.Id, TestContext.Current.CancellationToken))
            .Should().BeTrue("the tenant keeps the member that administers it");
    }

    /// <summary>
    /// Verifies that such a caller cannot read the tenant's membership - the roster a takeover would be
    /// planned from, and personal data of accounts that are none of their business.
    /// </summary>
    [Fact]
    public async Task Administration_Of_Another_Tenant_Does_Not_List_Members_Here()
    {
        var addressed = await CreateTenantAsync();
        await CreateFirstMemberAsync(addressed.Id);
        var (_, callerClient) = await CreateMemberAdministeringAnotherTenantAsync(addressed.Id);

        var (response, problem) = await callerClient
            .GETAsync<TenantMemberListEndpoint, TenantMemberListRequest, ProblemDetails>(
                new() { TenantId = addressed.Id });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.Should().ContainSingle();
        problem.Errors.First().Code.Should().Be(ErrorCodes.NotTenantMember);
    }

    /// <summary>
    /// Verifies the other half of the rule, so that the refusals above are read as the tenant being
    /// wrong rather than the surface being closed: the same call, made by a caller who holds the
    /// permission inside the tenant they address, is answered.
    /// </summary>
    [Fact]
    public async Task The_Tenants_Own_Administrator_Is_Admitted()
    {
        var addressed = await CreateTenantAsync();
        var administrator = await CreateFirstMemberAsync(addressed.Id);
        var member = await CreateTenantUserAsync(addressed.Id);
        var roleId = await CreateTenantRoleAsync(addressed.Id, Allow.Tenant_View);
        var administratorClient = await ClientForAsync(administrator.Username, addressed.Id);

        var (response, updated) = await administratorClient
            .PUTAsync<TenantMemberUpdateRolesEndpoint, TenantMemberUpdateRolesRequest, TenantMemberUpdateRolesResponse>(
                new() { TenantId = addressed.Id, UserId = member.Id, Roles = [roleId] });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        updated.Roles.Should().Equal([roleId]);
        (await GrantedRolesAsync(addressed.Id, member.Id)).Should().Equal([roleId]);
    }

    /// <summary>
    /// Creates the caller every refusal above is made by: an account holding a bare membership of the
    /// tenant addressed and administering a tenant of its own, signed in and acting in that other
    /// tenant, so its request carries the membership permissions while the tenant it names is one it
    /// may not administer.
    /// </summary>
    /// <param name="addressedTenantId">The tenant the caller is to be an ordinary member of.</param>
    /// <returns>The account, and a client acting as it inside its own tenant.</returns>
    private async Task<(User Caller, HttpClient Client)> CreateMemberAdministeringAnotherTenantAsync(Guid addressedTenantId)
    {
        var caller = await CreateTenantUserAsync(addressedTenantId);

        // Its first member, so the membership service grants it that tenant's administrator role -
        // the standing anybody can reach unaided by onboarding a tenant of their own.
        var ownTenant = await CreateTenantAsync();
        await MembershipService.AddAsync(ownTenant.Id, caller.Id, [], TestContext.Current.CancellationToken);

        return (caller, await ClientForAsync(caller.Username, ownTenant.Id));
    }

    /// <summary>
    /// Gives a tenant its first member, who is granted the tenant's administrator role, so that the
    /// tenant under test is administered by somebody other than the caller doing the addressing.
    /// </summary>
    /// <param name="tenantId">The tenant to give a first member to.</param>
    /// <returns>The account that became the tenant's first member.</returns>
    private async Task<User> CreateFirstMemberAsync(Guid tenantId)
    {
        var firstMember = await CreateAccountWithoutMembershipAsync();

        var result = await MembershipService.AddAsync(tenantId, firstMember.Id, [], TestContext.Current.CancellationToken);

        result.RoleIds.Should().ContainSingle("a tenant's first member is granted its administrator role, which is what keeps the tenant administered");
        return firstMember;
    }

    /// <summary>
    /// The identity of the role a tenant was provisioned with - the one holding tenant administration.
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
    /// The roles an account holds inside one named tenant, read from the assignments rather than from
    /// anything an endpoint reported.
    /// </summary>
    /// <param name="tenantId">The tenant the roles are read for.</param>
    /// <param name="userId">The account whose assignments are read.</param>
    /// <returns>The identifiers of the roles held there.</returns>
    private async Task<List<Guid>> GrantedRolesAsync(Guid tenantId, Guid userId)
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var tenantRoleIds = await DbContext.Roles
            .AcrossAllTenants()
            .AsNoTracking()
            .Where(role => role.TenantId == tenantId)
            .Select(role => role.Id)
            .ToListAsync(cancellationToken);

        return await DbContext.UserRoles
            .AsNoTracking()
            .Where(assignment => assignment.UserId == userId && tenantRoleIds.Contains(assignment.RoleId))
            .Select(assignment => assignment.RoleId)
            .ToListAsync(cancellationToken);
    }
}
