using Backend.Features.Tenancy.Core.Entities;

namespace Backend.Tests.Features.Tenancy.Endpoints.Tenants;

using Backend.Data.Entities;
using Backend.Features.Identity.Core.Entities;
using Backend.Features.Identity.Endpoints.Users;
using Backend.Features.Tenancy.Endpoints.Tenants;

/// <summary>
/// Tests for <see cref="TenantMemberUpdateRolesEndpoint"/>: replacing one member's roles inside one
/// tenant with exactly the set supplied, the change it records, the last-administrator guard, the
/// tenant whose roles it leaves alone, and every refusal it owes - a caller with no standing, a
/// member who is not there, another tenant's role, a suspended tenant and a tenant that is not there
/// (AC-017, AC-019, AC-020, AC-021, AC-086).
/// </summary>
/// <remarks>
/// <para>
/// The set supplied is the whole of the change: a role left out of it is withdrawn, a role named
/// twice is granted once, and an empty set leaves the member in the tenant holding nothing there. The
/// tests therefore read the member's assignments back from the database rather than trusting the
/// response, because what matters is what the account may do in the tenant afterwards.
/// </para>
/// <para>
/// Every tenant a test replaces roles in is given a member holding that tenant's administration
/// first, through <see cref="CreateFirstMemberAsync"/>, so the guard this endpoint answers to is
/// satisfied by somebody other than the member under test and the tests are about the replacement
/// rather than about the guard. The guard itself has its own test below, in which the member being
/// re-roled is the tenant's only administrator.
/// </para>
/// </remarks>
public class TenantMemberUpdateRolesTests(App app) : TenancyTestsBase(app)
{
    /// <summary>
    /// The soft-delete query filter's registered key, named so a read that has to see a membership
    /// that was removed can relax that one filter and leave every other in force.
    /// </summary>
    private const string SoftDeleteFilterKey = "SoftDelete";

    /// <summary>
    /// Verifies that a replacement leaves the member holding exactly the roles supplied - the role
    /// left out withdrawn and the role added granted - and that the change is recorded on the
    /// membership as the last thing that happened to it (AC-017, AC-078).
    /// </summary>
    [Fact]
    public async Task Valid_Input()
    {
        var tenant = await CreateTenantAsync();
        await CreateFirstMemberAsync(tenant.Id);
        var roleA = await CreateTenantRoleAsync(tenant.Id, Allow.Tenant_View);
        var roleB = await CreateTenantRoleAsync(tenant.Id, Allow.Role_View);
        var roleC = await CreateTenantRoleAsync(tenant.Id, Allow.User_View);
        var member = await CreateTenantUserAsync(tenant.Id, roleA, roleB);
        await SetPlatformAdminAuthTokenAsync();

        var before = (await MembershipRowsAsync(tenant.Id, member.Id)).Should().ContainSingle().Subject;

        var (response, updated) = await Client
            .PUTAsync<TenantMemberUpdateRolesEndpoint, TenantMemberUpdateRolesRequest, TenantMemberUpdateRolesResponse>(
                new() { TenantId = tenant.Id, UserId = member.Id, Roles = [roleB, roleC] });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        updated.Id.Should().Be(before.Id, "the answer names the membership that was re-roled rather than the account");
        updated.TenantId.Should().Be(tenant.Id);
        updated.UserId.Should().Be(member.Id);
        updated.Roles.Should().BeEquivalentTo(new[] { roleB, roleC });

        (await GrantedRolesAsync(tenant.Id, member.Id))
            .Should().BeEquivalentTo(new[] { roleB, roleC }, "the set supplied is the set held: one role was withdrawn and another granted");

        var after = (await MembershipRowsAsync(tenant.Id, member.Id)).Should().ContainSingle().Subject;

        after.CreatedAt.Should().Be(before.CreatedAt, "a replacement changes the membership rather than making a new one");
        after.UpdatedBy.Should().Be(TestUsers.PlatformAdminUserId, "the caller that made the change is recorded against the membership it changed");
        after.UpdatedAt!.Value.Should().BeAfter(before.UpdatedAt!.Value, "and it is recorded with the time it happened");
    }

    /// <summary>
    /// Verifies that a replacement that would leave the tenant with no member holding tenant
    /// administration is refused with a defined code, and that the member still holds the
    /// administration the request would have withdrawn - refused rather than corrected, so nothing of
    /// the request was applied (AC-019).
    /// </summary>
    [Fact]
    public async Task Cannot_Strip_Last_Administrator_Role()
    {
        var tenant = await CreateTenantAsync();
        var administrator = await CreateFirstMemberAsync(tenant.Id);
        var administratorRoleId = await TenantAdministratorRoleIdAsync(tenant.Id);
        var ordinaryRoleId = await CreateTenantRoleAsync(tenant.Id, Allow.Tenant_View);
        await SetPlatformAdminAuthTokenAsync();

        var (response, problem) = await Client
            .PUTAsync<TenantMemberUpdateRolesEndpoint, TenantMemberUpdateRolesRequest, ProblemDetails>(
                new() { TenantId = tenant.Id, UserId = administrator.Id, Roles = [ordinaryRoleId] });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.Should().ContainSingle();
        problem.Errors.First().Code.Should().Be(ErrorCodes.LastTenantAdministrator);

        (await GrantedRolesAsync(tenant.Id, administrator.Id))
            .Should().Equal([administratorRoleId], "the tenant is left exactly as it was, administrable by the member the request tried to strip");
    }

    /// <summary>
    /// Verifies that an empty role set is a valid replacement that leaves the member in the tenant
    /// holding nothing there, rather than a removal: the membership stands and the account may be
    /// given roles again without being added back (AC-017).
    /// </summary>
    [Fact]
    public async Task Empty_Set_Leaves_The_Member_In_The_Tenant()
    {
        var tenant = await CreateTenantAsync();
        await CreateFirstMemberAsync(tenant.Id);
        var member = await CreateTenantUserAsync(tenant.Id, await CreateTenantRoleAsync(tenant.Id, Allow.Tenant_View));
        await SetPlatformAdminAuthTokenAsync();

        var (response, updated) = await Client
            .PUTAsync<TenantMemberUpdateRolesEndpoint, TenantMemberUpdateRolesRequest, TenantMemberUpdateRolesResponse>(
                new() { TenantId = tenant.Id, UserId = member.Id, Roles = [] });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        updated.Roles.Should().BeEmpty();
        (await GrantedRolesAsync(tenant.Id, member.Id)).Should().BeEmpty("the member holds no role in this tenant");

        var membership = (await MembershipRowsAsync(tenant.Id, member.Id)).Should().ContainSingle().Subject;

        membership.IsDeleted.Should().BeFalse("the member is still a member, holding nothing rather than belonging nowhere");
        (await MembershipService.IsMemberAsync(tenant.Id, member.Id, TestContext.Current.CancellationToken)).Should().BeTrue();
    }

    /// <summary>
    /// Verifies that a set naming the same role twice grants it once, so what was asked for, what was
    /// stored and what is reported back are one and the same set (AC-017).
    /// </summary>
    [Fact]
    public async Task Duplicate_Roles_Are_Granted_Once()
    {
        var tenant = await CreateTenantAsync();
        await CreateFirstMemberAsync(tenant.Id);
        var roleId = await CreateTenantRoleAsync(tenant.Id, Allow.Tenant_View);
        var member = await CreateTenantUserAsync(tenant.Id);
        await SetPlatformAdminAuthTokenAsync();

        var (response, updated) = await Client
            .PUTAsync<TenantMemberUpdateRolesEndpoint, TenantMemberUpdateRolesRequest, TenantMemberUpdateRolesResponse>(
                new() { TenantId = tenant.Id, UserId = member.Id, Roles = [roleId, roleId] });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        updated.Roles.Should().Equal([roleId], "a role named twice is one role, reported as the single thing it is");
        (await GrantedRolesAsync(tenant.Id, member.Id)).Should().Equal([roleId]);
    }

    /// <summary>
    /// Verifies that a replacement reaches one tenant's assignments and no other's, so an account that
    /// belongs to several tenants can be re-roled in one of them without its standing in the rest being
    /// disturbed (AC-017).
    /// </summary>
    [Fact]
    public async Task Only_This_Tenants_Assignments_Are_Replaced()
    {
        var tenantA = await CreateTenantAsync();
        var tenantB = await CreateTenantAsync();
        await CreateFirstMemberAsync(tenantA.Id);
        await CreateFirstMemberAsync(tenantB.Id);
        var roleInA = await CreateTenantRoleAsync(tenantA.Id, Allow.Tenant_View);
        var newRoleInA = await CreateTenantRoleAsync(tenantA.Id, Allow.Role_View);
        var roleInB = await CreateTenantRoleAsync(tenantB.Id, Allow.User_View);
        var member = await CreateTenantUserAsync(tenantA.Id, roleInA);
        var cancellationToken = TestContext.Current.CancellationToken;
        await MembershipService.AddAsync(tenantB.Id, member.Id, [roleInB], cancellationToken);
        await SetPlatformAdminAuthTokenAsync();

        var (response, _) = await Client
            .PUTAsync<TenantMemberUpdateRolesEndpoint, TenantMemberUpdateRolesRequest, TenantMemberUpdateRolesResponse>(
                new() { TenantId = tenantA.Id, UserId = member.Id, Roles = [newRoleInA] });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await GrantedRolesAsync(tenantA.Id, member.Id)).Should().Equal([newRoleInA]);
        (await GrantedRolesAsync(tenantB.Id, member.Id)).Should().Equal([roleInB], "the member's standing in the other tenant lies outside the set being replaced");
    }

    /// <summary>
    /// Verifies that a replacement reaches the member's working session at its next renewal: the
    /// session stops being admitted to the operation the withdrawn role conferred, with no sign-in, no
    /// password change and no session ended (AC-020).
    /// </summary>
    /// <remarks>
    /// The renewal is the point at which the change lands. What a session may do is decided when its
    /// token is minted and trusted until that token is replaced, so the access token the member holds
    /// goes on carrying the withdrawn role's permissions for the rest of its validity - and the
    /// renewal, which reads the assignments as they stand, is what stops it.
    /// </remarks>
    [Fact]
    public async Task Replacement_Reaches_The_Member_On_Their_Next_Renewal()
    {
        var tenant = await CreateTenantAsync();
        await CreateFirstMemberAsync(tenant.Id);
        var roleId = await CreateTenantRoleAsync(tenant.Id, Allow.User_View);
        var member = await CreateTenantUserAsync(tenant.Id, roleId);
        var session = await SessionForAsync(member.Username, tenant.Id);

        var (before, _) = await session.Client.GETAsync<UserListEndpoint, UserListRequest, UserListResponse>(new());

        before.StatusCode.Should().Be(HttpStatusCode.OK, "the member holds the permission the endpoint requires");

        await SetPlatformAdminAuthTokenAsync();

        var (replaced, _) = await Client
            .PUTAsync<TenantMemberUpdateRolesEndpoint, TenantMemberUpdateRolesRequest, TenantMemberUpdateRolesResponse>(
                new() { TenantId = tenant.Id, UserId = member.Id, Roles = [] });

        replaced.StatusCode.Should().Be(HttpStatusCode.OK);

        await session.RenewAsync();

        var (after, _) = await session.Client.GETAsync<UserListEndpoint, UserListRequest, UserListResponse>(new());

        // A bare 403 from endpoint authorization: the member is still a member acting in a healthy
        // tenant, so what they are owed is the plain answer that they no longer hold the permission the
        // role conferred - not a statement about their session or their tenant.
        after.StatusCode.Should().Be(HttpStatusCode.Forbidden, "the withdrawn role stops being held once the session is renewed");
        after.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized, "the session is renewed rather than ended for the change to take effect");
    }

    /// <summary>
    /// Verifies that a request naming a role that is not one of this tenant's own is refused against
    /// the role set, and that the member's assignments are left exactly as they stood - so a request
    /// naming another tenant's role applies no part of itself (AC-086).
    /// </summary>
    [Fact]
    public async Task Role_Of_Another_Tenant_Is_Refused()
    {
        var tenant = await CreateTenantAsync();
        await CreateFirstMemberAsync(tenant.Id);
        var heldRoleId = await CreateTenantRoleAsync(tenant.Id, Allow.Tenant_View);
        var member = await CreateTenantUserAsync(tenant.Id, heldRoleId);
        var foreignTenant = await CreateTenantAsync();
        var foreignRoleId = await CreateTenantRoleAsync(foreignTenant.Id, Allow.Role_View);
        await SetPlatformAdminAuthTokenAsync();

        var (response, problem) = await Client
            .PUTAsync<TenantMemberUpdateRolesEndpoint, TenantMemberUpdateRolesRequest, ProblemDetails>(
                new() { TenantId = tenant.Id, UserId = member.Id, Roles = [foreignRoleId] });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.Should().ContainSingle();
        problem.Errors.First().Name.Should().Be("roles", "the caller is told which value was refused");
        problem.Errors.First().Code.Should().Be(ErrorCodes.ReferencedRecordNotFound);

        (await GrantedRolesAsync(tenant.Id, member.Id))
            .Should().Equal([heldRoleId], "the check runs before the replacement, so the member is left holding what they held");
    }

    /// <summary>
    /// Verifies that a caller who is neither a platform administrator nor a member of the tenant
    /// addressed is refused with a defined code, and that the member named is left untouched - holding
    /// the permission inside another tenant is not standing in this one (AC-021).
    /// </summary>
    [Fact]
    public async Task Non_Member_Is_Refused()
    {
        var addressed = await CreateTenantAsync();
        await CreateFirstMemberAsync(addressed.Id);
        var heldRoleId = await CreateTenantRoleAsync(addressed.Id, Allow.Tenant_View);
        var member = await CreateTenantUserAsync(addressed.Id, heldRoleId);

        var foreignTenant = await CreateTenantAsync();
        var foreignRoleId = await CreateTenantRoleAsync(foreignTenant.Id, Allow.TenantMember_UpdateRoles);
        var caller = await CreateTenantUserAsync(foreignTenant.Id, foreignRoleId);
        var callerClient = await ClientForAsync(caller.Username, foreignTenant.Id);

        var (response, problem) = await callerClient
            .PUTAsync<TenantMemberUpdateRolesEndpoint, TenantMemberUpdateRolesRequest, ProblemDetails>(
                new() { TenantId = addressed.Id, UserId = member.Id, Roles = [] });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.Should().ContainSingle();
        problem.Errors.First().Code.Should().Be(ErrorCodes.NotTenantMember);

        (await GrantedRolesAsync(addressed.Id, member.Id)).Should().Equal([heldRoleId], "the refusal happens before anything is read, let alone written");
    }

    /// <summary>
    /// Verifies that re-roling an account that holds no membership of the tenant is answered as a
    /// missing record rather than as a business refusal, which is also the answer an account that was
    /// removed from the tenant gets (AC-086).
    /// </summary>
    [Fact]
    public async Task Member_Not_Found()
    {
        var tenant = await CreateTenantAsync();
        await CreateFirstMemberAsync(tenant.Id);
        var account = await CreateAccountWithoutMembershipAsync();
        await SetPlatformAdminAuthTokenAsync();

        var (response, _) = await Client
            .PUTAsync<TenantMemberUpdateRolesEndpoint, TenantMemberUpdateRolesRequest, TenantMemberUpdateRolesResponse>(
                new() { TenantId = tenant.Id, UserId = account.Id, Roles = [] });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Verifies that a suspended tenant's membership is not administered: the request is refused with
    /// the code naming suspension, and the member's roles are left as they stood (AC-086).
    /// </summary>
    [Fact]
    public async Task Suspended_Tenant_Is_Refused()
    {
        var tenant = await CreateTenantAsync(TenantStatus.Suspended);
        var heldRoleId = await CreateTenantRoleAsync(tenant.Id, Allow.Tenant_View);
        var member = await CreateTenantUserAsync(tenant.Id, heldRoleId);
        await SetPlatformAdminAuthTokenAsync();

        var (response, problem) = await Client
            .PUTAsync<TenantMemberUpdateRolesEndpoint, TenantMemberUpdateRolesRequest, ProblemDetails>(
                new() { TenantId = tenant.Id, UserId = member.Id, Roles = [] });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.Should().ContainSingle();
        problem.Errors.First().Code.Should().Be(ErrorCodes.TenantSuspended);

        (await GrantedRolesAsync(tenant.Id, member.Id)).Should().Equal([heldRoleId]);
    }

    /// <summary>
    /// Verifies that a request naming a tenant that is not there is refused with the tenant code, which
    /// is also the answer a deleted tenant gets (AC-086).
    /// </summary>
    [Fact]
    public async Task Tenant_Not_Found()
    {
        await SetPlatformAdminAuthTokenAsync();

        var (response, problem) = await Client
            .PUTAsync<TenantMemberUpdateRolesEndpoint, TenantMemberUpdateRolesRequest, ProblemDetails>(
                new() { TenantId = Guid.NewGuid(), UserId = Guid.NewGuid(), Roles = [] });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.Should().ContainSingle();
        problem.Errors.First().Code.Should().Be(ErrorCodes.TenantNotFound);
    }

    /// <summary>
    /// Verifies that a request naming no tenant, no member or an empty role is refused as a validation
    /// failure naming the field, before anything is read (AC-086).
    /// </summary>
    [Fact]
    public async Task Missing_Fields_Are_Rejected()
    {
        await SetPlatformAdminAuthTokenAsync();

        var (missingTenant, tenantProblem) = await Client
            .PUTAsync<TenantMemberUpdateRolesEndpoint, TenantMemberUpdateRolesRequest, ProblemDetails>(
                new() { TenantId = Guid.Empty, UserId = Guid.NewGuid(), Roles = [] });

        var (missingMember, memberProblem) = await Client
            .PUTAsync<TenantMemberUpdateRolesEndpoint, TenantMemberUpdateRolesRequest, ProblemDetails>(
                new() { TenantId = Guid.NewGuid(), UserId = Guid.Empty, Roles = [] });

        var (emptyRole, roleProblem) = await Client
            .PUTAsync<TenantMemberUpdateRolesEndpoint, TenantMemberUpdateRolesRequest, ProblemDetails>(
                new() { TenantId = Guid.NewGuid(), UserId = Guid.NewGuid(), Roles = [Guid.Empty] });

        missingTenant.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        tenantProblem.Errors.Should().Contain(error => error.Name == "tenantId");

        missingMember.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        memberProblem.Errors.Should().Contain(error => error.Name == "userId");

        emptyRole.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        roleProblem.Errors.Should().Contain(error => error.Name == "roles[0]", "every entry of the role set is checked, so an empty one is reported where it sits");
    }

    /// <summary>
    /// Verifies that the guard protecting a tenant from being left unadministered counts the members
    /// who hold tenant administration rather than the members the tenant happens to have: the
    /// replacement is refused while the only holder of it is the member being re-roled, however many
    /// members remain, and is allowed the moment another member holds it (AC-019).
    /// </summary>
    [Fact]
    public async Task The_Guard_Counts_Administrators_Rather_Than_Members()
    {
        var tenant = await CreateTenantAsync();
        var administrator = await CreateFirstMemberAsync(tenant.Id);
        var ordinaryMember = await CreateTenantUserAsync(tenant.Id);
        var administratorRoleId = await TenantAdministratorRoleIdAsync(tenant.Id);
        var ordinaryRoleId = await CreateTenantRoleAsync(tenant.Id, Allow.Tenant_View);
        await SetPlatformAdminAuthTokenAsync();

        (await GrantedRolesAsync(tenant.Id, ordinaryMember.Id))
            .Should().BeEmpty("the member the tenant keeps holds no role in it, and so administers nothing");

        var (refused, problem) = await Client
            .PUTAsync<TenantMemberUpdateRolesEndpoint, TenantMemberUpdateRolesRequest, ProblemDetails>(
                new() { TenantId = tenant.Id, UserId = administrator.Id, Roles = [ordinaryRoleId] });

        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.Should().ContainSingle();
        problem.Errors.First().Code.Should().Be(
            ErrorCodes.LastTenantAdministrator,
            "the tenant keeps a member, but no member of it would hold tenant administration");

        (await GrantedRolesAsync(tenant.Id, administrator.Id))
            .Should().Equal([administratorRoleId], "the member the request would have demoted is left exactly as they stood");

        await TenantAuthorizationService.ReplaceTenantRoleAssignmentsAsync(
            tenant.Id, ordinaryMember.Id, [administratorRoleId], TestContext.Current.CancellationToken);

        var (allowed, updated) = await Client
            .PUTAsync<TenantMemberUpdateRolesEndpoint, TenantMemberUpdateRolesRequest, TenantMemberUpdateRolesResponse>(
                new() { TenantId = tenant.Id, UserId = administrator.Id, Roles = [ordinaryRoleId] });

        allowed.StatusCode.Should().Be(
            HttpStatusCode.OK,
            "the same replacement is permitted once another member holds the administration, so what the guard measures is the permission rather than the membership");
        updated.Roles.Should().Equal([ordinaryRoleId]);
        (await GrantedRolesAsync(tenant.Id, administrator.Id)).Should().Equal([ordinaryRoleId]);
    }

    /// <summary>
    /// Gives a tenant its first member, who is granted the tenant's administrator role by the
    /// membership service, so that the member a test then re-roles is not the tenant's only
    /// administrator and the last-administrator guard is satisfied by somebody else.
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
    /// The roles an account holds inside one named tenant, read from the assignments rather than from
    /// anything the endpoint reported.
    /// </summary>
    /// <param name="tenantId">The tenant the roles are read for.</param>
    /// <param name="userId">The account whose roles are read.</param>
    /// <returns>The identifiers of the roles it holds there.</returns>
    /// <remarks>
    /// The tenant's role identifiers are read first and stated in the predicate, because the
    /// assignments themselves are not tenant-scoped - a role carries the tenant, not the link that
    /// grants it - and a predicate that reached through <c>Role</c> would be filtering on a set the
    /// active scope has already narrowed.
    /// </remarks>
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

    /// <summary>
    /// Every membership row recorded for one account in one tenant, removed ones included, so a test
    /// can read the audit values a change was recorded with and tell a membership that was revoked from
    /// one that was erased.
    /// </summary>
    /// <param name="tenantId">The tenant the membership belongs to.</param>
    /// <param name="userId">The account the membership belongs to.</param>
    /// <returns>The retained membership rows for that pair.</returns>
    private async Task<List<TenantMembership>> MembershipRowsAsync(Guid tenantId, Guid userId)
        => await DbContext.TenantMemberships
            .AcrossAllTenants()
            .IgnoreQueryFilters([SoftDeleteFilterKey])
            .AsNoTracking()
            .Where(membership => membership.TenantId == tenantId && membership.UserId == userId)
            .ToListAsync(TestContext.Current.CancellationToken);
}
