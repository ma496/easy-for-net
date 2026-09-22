namespace Backend.Tests.Features.Tenancy.Endpoints.Tenants;

using Backend.ShareData.Entities;
using Backend.Features.Tenancy.Core.Entities;
using Backend.Features.Tenancy.Core;
using Backend.Features.Tenancy.Endpoints.Tenants;

/// <summary>
/// Tests for <see cref="TenantMemberAddEndpoint"/>: joining an existing account to a tenant with
/// exactly the roles named, the memberships it leaves untouched, the account it refuses to add twice,
/// the account it refuses to invent, the caller it refuses for want of standing, and every other
/// branch of the operation (AC-013 - AC-016, AC-021, AC-078, AC-086, AC-106, AC-107).
/// </summary>
/// <remarks>
/// <para>
/// The tenant a test adds a member to is one it created, and the account it adds is one it created
/// without a membership, so nothing here depends on the seeded data beyond the caller's own standing.
/// </para>
/// <para>
/// A tenant's first member is also granted that tenant's administrator role, which is what makes a
/// tenant administrable from inside it from the moment anybody is in it. That is real behaviour with
/// its own test below; the tests that prove the granted set is exactly the set asked for arrange the
/// tenant's first member first, through <see cref="GiveTenantItsFirstMemberAsync"/>, so that the
/// member they are actually testing is a later one.
/// </para>
/// </remarks>
public class TenantMemberAddTests(App app) : TenancyTestsBase(app)
{
    /// <summary>
    /// The soft-delete query filter's registered key, named so a read that has to see a membership
    /// that was removed can relax that one filter and leave every other in force.
    /// </summary>
    private const string SoftDeleteFilterKey = "SoftDelete";

    /// <summary>
    /// Verifies that adding an existing account to a tenant creates the membership and grants exactly
    /// the tenant roles the request named - no more, which would be authority nobody asked for, and no
    /// fewer, which would be a member unable to do the job they were added for (AC-014).
    /// </summary>
    [Fact]
    public async Task Valid_Input()
    {
        var tenant = await CreateTenantAsync();
        await GiveTenantItsFirstMemberAsync(tenant.Id);
        var roleA = await CreateTenantRoleAsync(tenant.Id, Allow.Tenant_View);
        var roleB = await CreateTenantRoleAsync(tenant.Id, Allow.TenantMember_View);
        var account = await CreateAccountWithoutMembershipAsync();
        await SetPlatformAdminAuthTokenAsync();

        var (response, added) = await Client
            .POSTAsync<TenantMemberAddEndpoint, TenantMemberAddRequest, TenantMemberAddResponse>(new()
            {
                TenantId = tenant.Id,
                UserId = account.Id,
                Roles = [roleA, roleB]
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        added.Id.Should().NotBeEmpty("the membership created is reported, so a later change of this member can address it");
        added.TenantId.Should().Be(tenant.Id);
        added.UserId.Should().Be(account.Id);
        added.Roles.Should().BeEquivalentTo(new[] { roleA, roleB });

        // Read from the database rather than from the response: the answer is about what the account
        // holds in the tenant now, not about what the endpoint said it granted.
        (await GrantedRolesAsync(tenant.Id, account.Id))
            .Should().BeEquivalentTo(new[] { roleA, roleB }, "the member holds exactly the roles the request named, and the tenant's own administrator role is not among them");

        var membership = (await MembershipRowsAsync(tenant.Id, account.Id)).Should().ContainSingle().Subject;

        membership.IsDeleted.Should().BeFalse();
    }

    /// <summary>
    /// Verifies that the first member of a tenant is granted that tenant's administrator role on top
    /// of the set asked for, so a tenant can always be administered from inside it and the
    /// last-administrator guard can never be violated the moment a tenant gains its first member
    /// (AC-086).
    /// </summary>
    [Fact]
    public async Task First_Member_Is_Also_Granted_Tenant_Administration()
    {
        var tenant = await CreateTenantAsync();
        var account = await CreateAccountWithoutMembershipAsync();
        await SetPlatformAdminAuthTokenAsync();

        var (response, added) = await Client
            .POSTAsync<TenantMemberAddEndpoint, TenantMemberAddRequest, TenantMemberAddResponse>(new()
            {
                TenantId = tenant.Id,
                UserId = account.Id,
                Roles = []
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        added.Roles.Should().ContainSingle("the tenant had no member, so its administrator role is added to the empty set that was asked for");
        (await GrantedRolesAsync(tenant.Id, account.Id)).Should().BeEquivalentTo(added.Roles);

        var administratorRole = await DbContext.Roles
            .AcrossAllTenants()
            .AsNoTracking()
            .SingleAsync(role => role.Id == added.Roles[0], TestContext.Current.CancellationToken);

        administratorRole.TenantId.Should().Be(tenant.Id, "the role granted is the tenant's own, not another tenant's and not the platform's");
        administratorRole.SystemCreated.Should().BeTrue("it is the role the tenant was provisioned with rather than one somebody defined");
    }

    /// <summary>
    /// Verifies that adding an account that already belongs to the tenant is refused with a defined
    /// code, naming the account as what was wrong with the request, and that the refusal creates no
    /// second membership (AC-015).
    /// </summary>
    [Fact]
    public async Task Duplicate_Membership()
    {
        var tenant = await CreateTenantAsync();
        await GiveTenantItsFirstMemberAsync(tenant.Id);
        var account = await CreateAccountWithoutMembershipAsync();
        await SetPlatformAdminAuthTokenAsync();

        var (first, _) = await Client
            .POSTAsync<TenantMemberAddEndpoint, TenantMemberAddRequest, TenantMemberAddResponse>(new()
            {
                TenantId = tenant.Id,
                UserId = account.Id,
                Roles = []
            });

        var (second, problem) = await Client
            .POSTAsync<TenantMemberAddEndpoint, TenantMemberAddRequest, ProblemDetails>(new()
            {
                TenantId = tenant.Id,
                UserId = account.Id,
                Roles = []
            });

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.Should().ContainSingle();
        problem.Errors.First().Name.Should().Be("userId", "the caller is told which value was refused");
        problem.Errors.First().Code.Should().Be(ErrorCodes.DuplicateTenantMembership);

        (await MembershipRowsAsync(tenant.Id, account.Id))
            .Should().ContainSingle("a refused request writes nothing, so the membership stands exactly as it did");
    }

    /// <summary>
    /// Verifies that a membership cannot be created for an account that does not exist: the request is
    /// refused with a defined code and no membership is stored, because a membership joins an existing
    /// account to a tenant rather than bringing one into being (AC-016).
    /// </summary>
    [Fact]
    public async Task Unknown_User()
    {
        var tenant = await CreateTenantAsync();
        await GiveTenantItsFirstMemberAsync(tenant.Id);
        var unknownUserId = Guid.NewGuid();
        await SetPlatformAdminAuthTokenAsync();

        var (response, problem) = await Client
            .POSTAsync<TenantMemberAddEndpoint, TenantMemberAddRequest, ProblemDetails>(new()
            {
                TenantId = tenant.Id,
                UserId = unknownUserId,
                Roles = []
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.Should().ContainSingle();
        problem.Errors.First().Name.Should().Be("userId");
        problem.Errors.First().Code.Should().Be(ErrorCodes.UserNotFound);

        (await MembershipRowsAsync(tenant.Id, unknownUserId)).Should().BeEmpty();
    }

    /// <summary>
    /// Verifies that a caller who is neither a platform administrator nor a member of the tenant
    /// addressed is refused with a defined code, and that nothing is written - so the membership
    /// surface cannot be used to join an account to a tenant the caller has no standing in
    /// (AC-021).
    /// </summary>
    /// <remarks>
    /// The caller holds the permission the endpoint declares inside its own tenant, so what refuses it
    /// is standing rather than authority.
    /// </remarks>
    [Fact]
    public async Task Non_Member_Is_Refused()
    {
        var addressed = await CreateTenantAsync();
        await GiveTenantItsFirstMemberAsync(addressed.Id);

        var foreignTenant = await CreateTenantAsync();
        var foreignRoleId = await CreateTenantRoleAsync(foreignTenant.Id, Allow.TenantMember_Add);
        var caller = await CreateTenantUserAsync(foreignTenant.Id, foreignRoleId);
        var target = await CreateAccountWithoutMembershipAsync();

        var callerClient = await ClientForAsync(caller.Username, foreignTenant.Id);

        var (response, problem) = await callerClient
            .POSTAsync<TenantMemberAddEndpoint, TenantMemberAddRequest, ProblemDetails>(new()
            {
                TenantId = addressed.Id,
                UserId = target.Id,
                Roles = []
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.Should().ContainSingle();
        problem.Errors.First().Code.Should().Be(ErrorCodes.NotTenantMember, "holding the permission in another tenant is not standing in this one");

        (await MembershipRowsAsync(addressed.Id, target.Id)).Should().BeEmpty("the standing check runs before anything is read, let alone written");
    }

    /// <summary>
    /// Verifies that an account's memberships are held independently, so removing it from one tenant
    /// leaves its membership of another, and the roles it holds there, exactly as they were
    /// (AC-013).
    /// </summary>
    [Fact]
    public async Task Memberships_Are_Independent()
    {
        var tenantA = await CreateTenantAsync();
        var tenantB = await CreateTenantAsync();
        await GiveTenantItsFirstMemberAsync(tenantA.Id);
        await GiveTenantItsFirstMemberAsync(tenantB.Id);
        var roleA = await CreateTenantRoleAsync(tenantA.Id, Allow.Tenant_View);
        var roleB = await CreateTenantRoleAsync(tenantB.Id, Allow.Role_View);
        var account = await CreateAccountWithoutMembershipAsync();

        var cancellationToken = TestContext.Current.CancellationToken;
        await MembershipService.AddAsync(tenantA.Id, account.Id, [roleA], cancellationToken);
        await MembershipService.AddAsync(tenantB.Id, account.Id, [roleB], cancellationToken);

        var beforeInB = await MembershipRowsAsync(tenantB.Id, account.Id);
        var outcome = await MembershipService.RemoveAsync(tenantA.Id, account.Id, cancellationToken);

        outcome.Should().Be(TenantMembershipChangeOutcome.Applied);
        (await MembershipService.IsMemberAsync(tenantA.Id, account.Id, cancellationToken)).Should().BeFalse("the membership that was removed is gone");
        (await MembershipService.IsMemberAsync(tenantB.Id, account.Id, cancellationToken)).Should().BeTrue("and the other one is untouched by it");

        (await GrantedRolesAsync(tenantB.Id, account.Id)).Should().BeEquivalentTo(new[] { roleB }, "the roles held in the other tenant survive the removal in this one");

        var afterInB = await MembershipRowsAsync(tenantB.Id, account.Id);
        afterInB.Should().ContainSingle();
        afterInB[0].Id.Should().Be(beforeInB[0].Id);
        afterInB[0].IsDeleted.Should().BeFalse();
        afterInB[0].UpdatedAt.Should().Be(beforeInB[0].UpdatedAt, "the other membership is not even touched, let alone changed");
    }

    /// <summary>
    /// Verifies that an account that was removed from a tenant can be added to it again, that the
    /// membership it gains is a new one, and that it takes exactly the roles the second request named
    /// rather than inheriting anything from the membership it held before (AC-106).
    /// </summary>
    [Fact]
    public async Task Re_Add_After_Removal()
    {
        var tenant = await CreateTenantAsync();
        await GiveTenantItsFirstMemberAsync(tenant.Id);
        var firstRoleId = await CreateTenantRoleAsync(tenant.Id, Allow.Tenant_View);
        var account = await CreateAccountWithoutMembershipAsync();
        await SetPlatformAdminAuthTokenAsync();

        var (_, first) = await Client
            .POSTAsync<TenantMemberAddEndpoint, TenantMemberAddRequest, TenantMemberAddResponse>(new()
            {
                TenantId = tenant.Id,
                UserId = account.Id,
                Roles = [firstRoleId]
            });

        await MembershipService.RemoveAsync(tenant.Id, account.Id, TestContext.Current.CancellationToken);

        // A different role for the second membership, so an assignment left over from the first would
        // be visible as a role that was never asked for this time.
        var secondRoleId = await CreateTenantRoleAsync(tenant.Id, Allow.Role_View);

        var (reAddResponse, reAdded) = await Client
            .POSTAsync<TenantMemberAddEndpoint, TenantMemberAddRequest, TenantMemberAddResponse>(new()
            {
                TenantId = tenant.Id,
                UserId = account.Id,
                Roles = [secondRoleId]
            });

        reAddResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        reAdded.Id.Should().NotBe(first.Id, "the rejoin is a new membership rather than the removed row being brought back");
        (await MembershipService.IsMemberAsync(tenant.Id, account.Id, TestContext.Current.CancellationToken)).Should().BeTrue();
        (await GrantedRolesAsync(tenant.Id, account.Id))
            .Should().BeEquivalentTo(new[] { secondRoleId }, "the new membership takes exactly the roles this request named and inherits nothing from the one before it");
    }

    /// <summary>
    /// Verifies that a membership which was removed does not make the account look like an existing
    /// member - so it does not block the rejoin - and that only the live membership that rejoin
    /// created is what a further attempt is refused against (AC-107).
    /// </summary>
    /// <remarks>
    /// The second half matters as much as the first: a comparison that simply ignored removed rows
    /// would also have to be shown to still refuse a duplicate of a membership that stands.
    /// </remarks>
    [Fact]
    public async Task Removed_Membership_Does_Not_Block_Re_Add()
    {
        var tenant = await CreateTenantAsync();
        await GiveTenantItsFirstMemberAsync(tenant.Id);
        var account = await CreateAccountWithoutMembershipAsync();
        await SetPlatformAdminAuthTokenAsync();

        var (_, _) = await Client
            .POSTAsync<TenantMemberAddEndpoint, TenantMemberAddRequest, TenantMemberAddResponse>(new()
            {
                TenantId = tenant.Id,
                UserId = account.Id,
                Roles = []
            });

        await MembershipService.RemoveAsync(tenant.Id, account.Id, TestContext.Current.CancellationToken);

        var (reAddResponse, reAdded) = await Client
            .POSTAsync<TenantMemberAddEndpoint, TenantMemberAddRequest, TenantMemberAddResponse>(new()
            {
                TenantId = tenant.Id,
                UserId = account.Id,
                Roles = []
            });

        reAddResponse.StatusCode.Should().Be(HttpStatusCode.OK, "a removed membership is not a membership, so it is not what a duplicate comparison sees");

        var (third, thirdProblem) = await Client
            .POSTAsync<TenantMemberAddEndpoint, TenantMemberAddRequest, ProblemDetails>(new()
            {
                TenantId = tenant.Id,
                UserId = account.Id,
                Roles = []
            });

        thirdProblem.Errors.First().Code.Should().Be(ErrorCodes.DuplicateTenantMembership, "the membership the rejoin made is live, and a second one is refused against it");
        third.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var rows = await MembershipRowsAsync(tenant.Id, account.Id);

        rows.Should().HaveCount(2, "the removed row is retained beside the new one rather than replaced by it");
        rows.Count(row => row.IsDeleted).Should().Be(1);
        rows.Count(row => !row.IsDeleted && row.Id == reAdded.Id).Should().Be(1);
    }

    /// <summary>
    /// Verifies that a membership records who created it and when, so the tenant has its own history
    /// of when an account joined it and on whose authority (AC-078).
    /// </summary>
    [Fact]
    public async Task Records_Audit_Fields()
    {
        var tenant = await CreateTenantAsync();
        await GiveTenantItsFirstMemberAsync(tenant.Id);
        var account = await CreateAccountWithoutMembershipAsync();
        await SetPlatformAdminAuthTokenAsync();

        var before = DateTime.UtcNow;
        var (response, added) = await Client
            .POSTAsync<TenantMemberAddEndpoint, TenantMemberAddRequest, TenantMemberAddResponse>(new()
            {
                TenantId = tenant.Id,
                UserId = account.Id,
                Roles = []
            });
        var after = DateTime.UtcNow;

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var membership = (await MembershipRowsAsync(tenant.Id, account.Id)).Should().ContainSingle().Subject;

        membership.Id.Should().Be(added.Id);
        membership.CreatedBy.Should().Be(TestUsers.PlatformAdminUserId, "the caller that made the member is recorded on the membership it made");
        membership.CreatedAt.Should().BeOnOrAfter(before.AddSeconds(-1)).And.BeOnOrBefore(after.AddSeconds(1));
        membership.UpdatedBy.Should().BeNull("nothing has updated it since it was created");
        membership.UpdatedAt.Should().NotBeNull("a row carries its creation as its last change until something changes it");
    }

    /// <summary>
    /// Verifies that adding a member to a tenant that does not exist is refused with a defined code,
    /// which is also the answer a deleted tenant gets (AC-086).
    /// </summary>
    [Fact]
    public async Task Tenant_Not_Found()
    {
        var account = await CreateAccountWithoutMembershipAsync();
        await SetPlatformAdminAuthTokenAsync();

        var (response, problem) = await Client
            .POSTAsync<TenantMemberAddEndpoint, TenantMemberAddRequest, ProblemDetails>(new()
            {
                TenantId = Guid.NewGuid(),
                UserId = account.Id,
                Roles = []
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.Should().ContainSingle();
        problem.Errors.First().Code.Should().Be(ErrorCodes.TenantNotFound);
    }

    /// <summary>
    /// Verifies that nothing is added to a suspended tenant: the request is refused with the code
    /// naming suspension, and no membership is written, because a tenant that is out of service is not
    /// administered while it is (AC-086).
    /// </summary>
    [Fact]
    public async Task Suspended_Tenant_Is_Refused()
    {
        var tenant = await CreateTenantAsync(TenantStatus.Suspended);
        var account = await CreateAccountWithoutMembershipAsync();
        await SetPlatformAdminAuthTokenAsync();

        var (response, problem) = await Client
            .POSTAsync<TenantMemberAddEndpoint, TenantMemberAddRequest, ProblemDetails>(new()
            {
                TenantId = tenant.Id,
                UserId = account.Id,
                Roles = []
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.Should().ContainSingle();
        problem.Errors.First().Code.Should().Be(ErrorCodes.TenantSuspended);

        (await MembershipRowsAsync(tenant.Id, account.Id)).Should().BeEmpty("the refusal happens before anything is written inside the tenant");
    }

    /// <summary>
    /// Verifies that a request naming a role that is not one of this tenant's own is refused against
    /// the role set, and that no membership is created - so a request naming another tenant's role
    /// cannot leave a member holding part of the set it asked for (AC-086).
    /// </summary>
    [Fact]
    public async Task Role_Of_Another_Tenant_Is_Refused()
    {
        var tenant = await CreateTenantAsync();
        await GiveTenantItsFirstMemberAsync(tenant.Id);
        var foreignTenant = await CreateTenantAsync();
        var foreignRoleId = await CreateTenantRoleAsync(foreignTenant.Id, Allow.Tenant_View);
        var account = await CreateAccountWithoutMembershipAsync();
        await SetPlatformAdminAuthTokenAsync();

        var (response, problem) = await Client
            .POSTAsync<TenantMemberAddEndpoint, TenantMemberAddRequest, ProblemDetails>(new()
            {
                TenantId = tenant.Id,
                UserId = account.Id,
                Roles = [foreignRoleId]
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.Should().ContainSingle();
        problem.Errors.First().Name.Should().Be("roles", "the caller is told which value was refused");
        problem.Errors.First().Code.Should().Be(ErrorCodes.ReferencedRecordNotFound);

        (await MembershipRowsAsync(tenant.Id, account.Id)).Should().BeEmpty("the role check runs before the membership is written, so nothing is left half-granted");
    }

    /// <summary>
    /// Verifies that a request naming no tenant, no account or an empty role is refused as a
    /// validation failure naming the field, before anything is read (AC-086).
    /// </summary>
    [Fact]
    public async Task Missing_Fields_Are_Rejected()
    {
        await SetPlatformAdminAuthTokenAsync();

        var (missingTenant, tenantProblem) = await Client
            .POSTAsync<TenantMemberAddEndpoint, TenantMemberAddRequest, ProblemDetails>(new()
            {
                TenantId = Guid.Empty,
                UserId = Guid.NewGuid(),
                Roles = []
            });

        var (missingUser, userProblem) = await Client
            .POSTAsync<TenantMemberAddEndpoint, TenantMemberAddRequest, ProblemDetails>(new()
            {
                TenantId = Guid.NewGuid(),
                UserId = Guid.Empty,
                Roles = []
            });

        var (emptyRole, roleProblem) = await Client
            .POSTAsync<TenantMemberAddEndpoint, TenantMemberAddRequest, ProblemDetails>(new()
            {
                TenantId = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                Roles = [Guid.Empty]
            });

        missingTenant.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        tenantProblem.Errors.Should().Contain(error => error.Name == "tenantId");

        missingUser.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        userProblem.Errors.Should().Contain(error => error.Name == "userId");

        emptyRole.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        roleProblem.Errors.Should().Contain(error => error.Name == "roles[0]", "every entry of the role set is checked, so an empty one is reported where it sits");
    }

    /// <summary>
    /// Gives a tenant its first member through the membership service, so that a later member is not
    /// the tenant's first and its granted set is therefore exactly the set the request names.
    /// </summary>
    /// <param name="tenantId">The tenant to give a first member to.</param>
    /// <remarks>
    /// The account given is one the test created and holds no other membership, so nothing shared is
    /// written to. The assertion is a precondition of every test that calls this rather than a
    /// finding of its own: if a tenant's first member stopped taking its administrator role, the
    /// tests that rely on the second member's set being exact would still pass while proving nothing.
    /// </remarks>
    private async Task GiveTenantItsFirstMemberAsync(Guid tenantId)
    {
        var firstMember = await CreateAccountWithoutMembershipAsync();

        var result = await MembershipService.AddAsync(tenantId, firstMember.Id, [], TestContext.Current.CancellationToken);

        result.RoleIds.Should().ContainSingle("a tenant's first member is granted its administrator role, which is what keeps later members' granted sets exact");
    }

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
    /// can tell a membership that was revoked from one that was erased and can count what a refused
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
