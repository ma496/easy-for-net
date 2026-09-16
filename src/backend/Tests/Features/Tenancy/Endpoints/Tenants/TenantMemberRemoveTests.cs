namespace Backend.Tests.Features.Tenancy.Endpoints.Tenants;

using Backend.Data.Entities;
using Backend.Features.Identity.Core.Entities;
using Backend.Features.Identity.Endpoints.Users;
using Backend.Features.Tenancy.Endpoints.Tenants;

/// <summary>
/// Tests for <see cref="TenantMemberRemoveEndpoint"/>: revoking one account's membership of one
/// tenant, the access that goes with it on the member's next request, the account and its other
/// memberships that survive it, the last-administrator guard, the row that is retained rather than
/// erased, and every refusal it owes (AC-018, AC-019, AC-020, AC-021, AC-079, AC-086).
/// </summary>
/// <remarks>
/// <para>
/// A removal is a withdrawal of access rather than a deletion of anything: the membership row stays,
/// soft-deleted, the user account stays, and the account's memberships of every other tenant stay
/// exactly as they were. The tests therefore assert on all three - what the member can no longer do in
/// this tenant, what still exists, and what is untouched elsewhere - because a removal that also
/// deleted the account or its other memberships would satisfy the first of those alone.
/// </para>
/// <para>
/// Every tenant a test removes a member from is given a member holding that tenant's administration
/// first, through <see cref="CreateFirstMemberAsync"/>, so the guard is satisfied by somebody other
/// than the member under test. The guard itself has its own test below, in which the member being
/// removed is the tenant's only administrator.
/// </para>
/// </remarks>
public class TenantMemberRemoveTests(App app) : TenancyTestsBase(app)
{
    /// <summary>
    /// The soft-delete query filter's registered key, named so a read that has to see a retained
    /// membership can relax that one filter and leave every other in force.
    /// </summary>
    private const string SoftDeleteFilterKey = "SoftDelete";

    /// <summary>
    /// Verifies that removing a member revokes their membership and their access to the tenant's data
    /// on their next request, while leaving the account and its membership of another tenant intact -
    /// and without a sign-in, a password change or a session being ended (AC-018, AC-020).
    /// </summary>
    [Fact]
    public async Task Valid_Input()
    {
        var removedTenant = await CreateTenantAsync();
        var remainingTenant = await CreateTenantAsync();
        await CreateFirstMemberAsync(removedTenant.Id);
        await CreateFirstMemberAsync(remainingTenant.Id);
        var roleInRemoved = await CreateTenantRoleAsync(removedTenant.Id, Allow.User_View);
        var roleInRemaining = await CreateTenantRoleAsync(remainingTenant.Id, Allow.User_View);
        var member = await CreateTenantUserAsync(removedTenant.Id, roleInRemoved);
        var cancellationToken = TestContext.Current.CancellationToken;
        await MembershipService.AddAsync(remainingTenant.Id, member.Id, [roleInRemaining], cancellationToken);

        // Both clients are made before the removal, because a client's identity is established by
        // selecting a tenant and the tenant about to be removed can no longer be selected afterwards.
        var removedClient = await ClientForAsync(member.Username, removedTenant.Id);
        var remainingClient = await ClientForAsync(member.Username, remainingTenant.Id);

        await SetAuthTokenAsync();

        var (admitted, _) = await removedClient.GETAsync<UserListEndpoint, UserListRequest, UserListResponse>(new());

        admitted.StatusCode.Should().Be(HttpStatusCode.OK, "the member holds the permission in this tenant, and belongs to it");

        var (response, removed) = await App.Client
            .DELETEAsync<TenantMemberRemoveEndpoint, TenantMemberRemoveRequest, TenantMemberRemoveResponse>(
                new() { TenantId = removedTenant.Id, UserId = member.Id });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        removed.Success.Should().BeTrue();

        var (refused, refusal) = await removedClient.GETAsync<UserListEndpoint, UserListRequest, ProblemDetails>(new());

        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden, "the membership that admitted the member is gone, and the tenant is still in service");
        refused.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized, "the removal ends no session and asks for no credentials");
        refusal.Errors.Should().ContainSingle();
        refusal.Errors.First().Code.Should().Be(ErrorCodes.TenantMembershipRevoked);

        // The other tenant is untouched in every part: the membership stands, the roles held there stand,
        // and a session acting in it goes on being answered.
        (await MembershipService.IsMemberAsync(remainingTenant.Id, member.Id, cancellationToken))
            .Should().BeTrue("a membership of one tenant is not a membership of another");
        (await GrantedRolesAsync(remainingTenant.Id, member.Id)).Should().Equal([roleInRemaining]);

        var (stillAdmitted, _) = await remainingClient.GETAsync<UserListEndpoint, UserListRequest, UserListResponse>(new());

        stillAdmitted.StatusCode.Should().Be(HttpStatusCode.OK, "removing the member from one tenant does not invalidate their access to another");
    }

    /// <summary>
    /// Verifies that removing the member who is the tenant's only holder of tenant administration is
    /// refused with a defined code, and that the membership is left active with the roles it held -
    /// refused rather than corrected, so nothing of the removal was applied (AC-019).
    /// </summary>
    [Fact]
    public async Task Cannot_Remove_Last_Administrator()
    {
        var tenant = await CreateTenantAsync();
        var administrator = await CreateFirstMemberAsync(tenant.Id);
        var administratorRoleId = await TenantAdministratorRoleIdAsync(tenant.Id);
        await SetAuthTokenAsync();

        var (response, problem) = await App.Client
            .DELETEAsync<TenantMemberRemoveEndpoint, TenantMemberRemoveRequest, ProblemDetails>(
                new() { TenantId = tenant.Id, UserId = administrator.Id });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.Should().ContainSingle();
        problem.Errors.First().Code.Should().Be(ErrorCodes.LastTenantAdministrator);

        var membership = (await MembershipRowsAsync(tenant.Id, administrator.Id)).Should().ContainSingle().Subject;

        membership.IsDeleted.Should().BeFalse("the membership the removal was refused for is still the membership the account holds");
        (await GrantedRolesAsync(tenant.Id, administrator.Id))
            .Should().Equal([administratorRoleId], "and the tenant is left administrable by the member the removal would have taken it from");
    }

    /// <summary>
    /// Verifies that removing a tenant administrator is permitted while another member holds tenant
    /// administration - the guard is about the tenant never being left unadministered, not about any
    /// one member being indispensable (AC-019).
    /// </summary>
    [Fact]
    public async Task Removing_A_Tenant_Administrator_Is_Allowed_When_Another_Remains()
    {
        var tenant = await CreateTenantAsync();
        var firstAdministrator = await CreateFirstMemberAsync(tenant.Id);
        var secondAdministrator = await CreateTenantUserAsync(tenant.Id, await TenantAdministratorRoleIdAsync(tenant.Id));
        await SetAuthTokenAsync();

        var (response, removed) = await App.Client
            .DELETEAsync<TenantMemberRemoveEndpoint, TenantMemberRemoveRequest, TenantMemberRemoveResponse>(
                new() { TenantId = tenant.Id, UserId = firstAdministrator.Id });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        removed.Success.Should().BeTrue();

        var cancellationToken = TestContext.Current.CancellationToken;

        (await MembershipService.IsMemberAsync(tenant.Id, firstAdministrator.Id, cancellationToken)).Should().BeFalse();
        (await MembershipService.IsMemberAsync(tenant.Id, secondAdministrator.Id, cancellationToken))
            .Should().BeTrue("the administration the tenant still holds is what the removal was measured against");
    }

    /// <summary>
    /// Verifies that a removal soft-deletes the membership: the row is retained carrying the removal
    /// and the time it happened, the roles held inside the tenant are withdrawn with it, and no query
    /// of the tenant's membership finds it any more (AC-079).
    /// </summary>
    [Fact]
    public async Task Removal_Is_Soft()
    {
        var tenant = await CreateTenantAsync();
        var administrator = await CreateFirstMemberAsync(tenant.Id);
        var member = await CreateTenantUserAsync(tenant.Id, await CreateTenantRoleAsync(tenant.Id, Allow.Tenant_View));
        await SetAuthTokenAsync();

        var (response, removed) = await App.Client
            .DELETEAsync<TenantMemberRemoveEndpoint, TenantMemberRemoveRequest, TenantMemberRemoveResponse>(
                new() { TenantId = tenant.Id, UserId = member.Id });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        removed.Success.Should().BeTrue();

        var retained = (await MembershipRowsAsync(tenant.Id, member.Id)).Should().ContainSingle().Subject;

        retained.IsDeleted.Should().BeTrue("a removal is recorded on the row rather than performed on it");
        retained.DeletedAt.Should().NotBeNull("and it is recorded with the time it happened");
        retained.UserId.Should().Be(member.Id);

        (await GrantedRolesAsync(tenant.Id, member.Id)).Should().BeEmpty("access to the tenant's data goes with the membership");

        var cancellationToken = TestContext.Current.CancellationToken;

        (await MembershipService.IsMemberAsync(tenant.Id, member.Id, cancellationToken)).Should().BeFalse();
        (await MembershipService.GetAsync(tenant.Id, member.Id, cancellationToken)).Should().BeNull("a removed membership is excluded from every membership read");

        // The account is what a removal does not touch: it stays, active and usable, because what ended
        // was its place in one tenant rather than the account itself.
        var account = await DbContext.Users
            .AsNoTracking()
            .SingleAsync(user => user.Id == member.Id, cancellationToken);

        account.IsActive.Should().BeTrue();

        var (listResponse, page) = await App.Client
            .GETAsync<TenantMemberListEndpoint, TenantMemberListRequest, TenantMemberListResponse>(
                new() { TenantId = tenant.Id, All = true });

        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        page.Items.Select(item => item.Id).Should().NotContain(member.Id, "a removed membership appears in no page of the tenant's members");
        page.Items.Select(item => item.Id).Should().Contain(administrator.Id, "and the members the tenant does have are still listed");
    }

    /// <summary>
    /// Verifies that a suspended tenant's membership is still administrable: suspension takes the
    /// tenant's work out of service, not the question of who belongs to it, so a member can be removed
    /// from one rather than being left in place until it is reactivated (AC-018).
    /// </summary>
    [Fact]
    public async Task Suspended_Tenant_Is_Still_Administrable()
    {
        var tenant = await CreateTenantAsync();
        await CreateFirstMemberAsync(tenant.Id);
        var member = await CreateTenantUserAsync(tenant.Id);
        await SetAuthTokenAsync();

        var (suspendResponse, _) = await App.Client
            .POSTAsync<TenantSuspendEndpoint, TenantSuspendRequest, TenantSuspendResponse>(new() { Id = tenant.Id });

        suspendResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var (response, removed) = await App.Client
            .DELETEAsync<TenantMemberRemoveEndpoint, TenantMemberRemoveRequest, TenantMemberRemoveResponse>(
                new() { TenantId = tenant.Id, UserId = member.Id });

        response.StatusCode.Should().Be(HttpStatusCode.OK, "suspension is deliberately no bar to a removal");
        removed.Success.Should().BeTrue();
        (await MembershipService.IsMemberAsync(tenant.Id, member.Id, TestContext.Current.CancellationToken)).Should().BeFalse();
    }

    /// <summary>
    /// Verifies that a caller who is neither a platform administrator nor a member of the tenant
    /// addressed is refused with a defined code, and that the membership named is left active - holding
    /// the permission inside another tenant is not standing in this one (AC-021).
    /// </summary>
    [Fact]
    public async Task Non_Member_Is_Refused()
    {
        var addressed = await CreateTenantAsync();
        await CreateFirstMemberAsync(addressed.Id);
        var member = await CreateTenantUserAsync(addressed.Id);

        var foreignTenant = await CreateTenantAsync();
        var foreignRoleId = await CreateTenantRoleAsync(foreignTenant.Id, Allow.TenantMember_Remove);
        var caller = await CreateTenantUserAsync(foreignTenant.Id, foreignRoleId);
        var callerClient = await ClientForAsync(caller.Username, foreignTenant.Id);

        var (response, problem) = await callerClient
            .DELETEAsync<TenantMemberRemoveEndpoint, TenantMemberRemoveRequest, ProblemDetails>(
                new() { TenantId = addressed.Id, UserId = member.Id });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.Should().ContainSingle();
        problem.Errors.First().Code.Should().Be(ErrorCodes.NotTenantMember);

        var membership = (await MembershipRowsAsync(addressed.Id, member.Id)).Should().ContainSingle().Subject;

        membership.IsDeleted.Should().BeFalse("the refusal happens before anything is read, let alone written");
    }

    /// <summary>
    /// Verifies that removing an account that holds no membership of the tenant is answered as a
    /// missing record rather than as a business refusal, which is also the answer a second attempt at
    /// the same removal gets (AC-086).
    /// </summary>
    [Fact]
    public async Task Member_Not_Found()
    {
        var tenant = await CreateTenantAsync();
        await CreateFirstMemberAsync(tenant.Id);
        var account = await CreateAccountWithoutMembershipAsync();
        await SetAuthTokenAsync();

        var (neverAMember, _) = await App.Client
            .DELETEAsync<TenantMemberRemoveEndpoint, TenantMemberRemoveRequest, TenantMemberRemoveResponse>(
                new() { TenantId = tenant.Id, UserId = account.Id });

        var member = await CreateTenantUserAsync(tenant.Id);
        var (removed, _) = await App.Client
            .DELETEAsync<TenantMemberRemoveEndpoint, TenantMemberRemoveRequest, TenantMemberRemoveResponse>(
                new() { TenantId = tenant.Id, UserId = member.Id });

        var (removedAgain, _) = await App.Client
            .DELETEAsync<TenantMemberRemoveEndpoint, TenantMemberRemoveRequest, TenantMemberRemoveResponse>(
                new() { TenantId = tenant.Id, UserId = member.Id });

        neverAMember.StatusCode.Should().Be(HttpStatusCode.NotFound);
        removed.StatusCode.Should().Be(HttpStatusCode.OK);
        removedAgain.StatusCode.Should().Be(HttpStatusCode.NotFound, "a member who was removed is indistinguishable from one who never joined");
    }

    /// <summary>
    /// Verifies that a request naming a tenant that is not there is refused with the tenant code, which
    /// is also the answer a deleted tenant gets (AC-086).
    /// </summary>
    [Fact]
    public async Task Tenant_Not_Found()
    {
        await SetAuthTokenAsync();

        var (response, problem) = await App.Client
            .DELETEAsync<TenantMemberRemoveEndpoint, TenantMemberRemoveRequest, ProblemDetails>(
                new() { TenantId = Guid.NewGuid(), UserId = Guid.NewGuid() });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.Should().ContainSingle();
        problem.Errors.First().Code.Should().Be(ErrorCodes.TenantNotFound);
    }

    /// <summary>
    /// Verifies that a request naming no tenant or no member is refused as a validation failure naming
    /// the field, before anything is read (AC-086).
    /// </summary>
    [Fact]
    public async Task Missing_Fields_Are_Rejected()
    {
        await SetAuthTokenAsync();

        var (missingTenant, tenantProblem) = await App.Client
            .DELETEAsync<TenantMemberRemoveEndpoint, TenantMemberRemoveRequest, ProblemDetails>(
                new() { TenantId = Guid.Empty, UserId = Guid.NewGuid() });

        var (missingMember, memberProblem) = await App.Client
            .DELETEAsync<TenantMemberRemoveEndpoint, TenantMemberRemoveRequest, ProblemDetails>(
                new() { TenantId = Guid.NewGuid(), UserId = Guid.Empty });

        missingTenant.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        tenantProblem.Errors.Should().Contain(error => error.Name == "tenantId");

        missingMember.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        memberProblem.Errors.Should().Contain(error => error.Name == "userId");
    }

    /// <summary>
    /// Verifies that the guard protecting a tenant from being left unadministered counts the members
    /// who hold tenant administration rather than the members the tenant happens to have: the removal
    /// is refused while the only holder of it is the one leaving, however many members remain, and is
    /// allowed the moment another member holds it (AC-019).
    /// </summary>
    [Fact]
    public async Task The_Guard_Counts_Administrators_Rather_Than_Members()
    {
        var tenant = await CreateTenantAsync();
        var administrator = await CreateFirstMemberAsync(tenant.Id);
        var ordinaryMember = await CreateTenantUserAsync(tenant.Id);
        var administratorRoleId = await TenantAdministratorRoleIdAsync(tenant.Id);
        await SetAuthTokenAsync();

        (await GrantedRolesAsync(tenant.Id, ordinaryMember.Id))
            .Should().BeEmpty("the member the tenant keeps holds no role in it, and so administers nothing");

        var (refused, problem) = await App.Client
            .DELETEAsync<TenantMemberRemoveEndpoint, TenantMemberRemoveRequest, ProblemDetails>(
                new() { TenantId = tenant.Id, UserId = administrator.Id });

        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.Should().ContainSingle();
        problem.Errors.First().Code.Should().Be(
            ErrorCodes.LastTenantAdministrator,
            "the tenant keeps a member, but no member of it would hold tenant administration");

        (await GrantedRolesAsync(tenant.Id, administrator.Id))
            .Should().Equal([administratorRoleId], "and the administrator the removal was refused for is left administering the tenant");

        await TenantAuthorizationService.ReplaceTenantRoleAssignmentsAsync(
            tenant.Id, ordinaryMember.Id, [administratorRoleId], TestContext.Current.CancellationToken);

        var (allowed, removed) = await App.Client
            .DELETEAsync<TenantMemberRemoveEndpoint, TenantMemberRemoveRequest, TenantMemberRemoveResponse>(
                new() { TenantId = tenant.Id, UserId = administrator.Id });

        allowed.StatusCode.Should().Be(
            HttpStatusCode.OK,
            "the same removal is permitted once another member holds the administration, so what the guard measures is the permission rather than the removal's own member");
        removed.Success.Should().BeTrue();
    }

    /// <summary>
    /// Gives a tenant its first member, who is granted the tenant's administrator role by the
    /// membership service, so that the member a test then removes is not the tenant's only
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
    /// can tell a membership that was revoked from one that was erased and can see what a refused
    /// request did not write.
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
