namespace Backend.Tests.Features.Tenancy.Endpoints.Tenants;

using Backend.Data.Entities;
using Backend.Features.Identity.Core.Entities;
using Backend.Features.Identity.Endpoints.Account;
using Backend.Features.Tenancy.Endpoints.Tenants;

/// <summary>
/// Tests for <see cref="TenantOnboardEndpoint"/>: an existing account giving itself a tenant, the
/// administrator standing and the active session it comes away with, the authority that does not come
/// with it, the rules it shares with the platform's own create surface, the unauthenticated caller it
/// refuses, and the tenants an account that already belongs to one may go on to create
/// (AC-133, AC-134, AC-135, AC-136, AC-137, AC-146).
/// </summary>
/// <remarks>
/// <para>
/// Onboarding is the door out of holding no usable tenant, so its callers here are accounts made for
/// the purpose: one with no membership at all, and one that already belongs to a tenant. Nothing
/// depends on the seeded data beyond the platform administrator's own token, which is the control that
/// shows a refusal is about the caller's authority rather than about the operation.
/// </para>
/// <para>
/// The session the response carries is what the caller acts with afterwards, so the tests attach that
/// very token to a client rather than signing in again: the session being re-established around the
/// new tenant is the claim being tested, and a second sign-in would answer a different question.
/// </para>
/// </remarks>
public class TenantOnboardTests(App app) : TenancyTestsBase(app)
{
    /// <summary>
    /// The soft-delete query filter's registered key, named so a read that has to see a membership
    /// that was removed can relax that one filter and leave every other in force.
    /// </summary>
    private const string SoftDeleteFilterKey = "SoftDelete";

    /// <summary>
    /// Verifies that an authenticated account onboards with a valid name and identifier: the tenant is
    /// created active, the creator is given an active membership of it and its system-created
    /// administrator role, and the session the answer carries makes it the caller's active tenant -
    /// all without a second sign-in (AC-133).
    /// </summary>
    [Fact]
    public async Task Valid_Input()
    {
        var creator = await CreateAccountWithoutMembershipAsync();
        var client = await ClientForAsync(creator.Username);
        var identifier = NewTenantIdentifier();

        var (response, onboarded) = await client
            .POSTAsync<TenantOnboardEndpoint, TenantOnboardRequest, TenantOnboardResponse>(
                new() { Name = "Onboarded Tenant", Identifier = identifier });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        onboarded.Id.Should().NotBeEmpty("the answer names the tenant that was created");
        onboarded.Status.Should().Be(TenantStatus.Active, "a tenant is born active whichever surface created it");
        onboarded.Identifier.Should().Be(identifier);
        onboarded.Session.UserId.Should().Be(creator.Id, "the session is the onboarding caller's own, re-established rather than newly signed in");
        onboarded.Session.AccessToken.Should().NotBeNullOrWhiteSpace();

        var membership = (await MembershipRowsAsync(onboarded.Id, creator.Id)).Should().ContainSingle().Subject;

        membership.TenantId.Should().Be(onboarded.Id);
        membership.IsDeleted.Should().BeFalse();
        membership.CreatedBy.Should().Be(creator.Id, "the membership onboarding writes is recorded as the creating account's");

        var administratorRoleId = await TenantAdministratorRoleIdAsync(onboarded.Id);

        (await GrantedRolesAsync(onboarded.Id, creator.Id))
            .Should().Equal([administratorRoleId], "the creator is given the tenant's own administrator role and nothing else");

        // Acting on the token the answer carried: the tenant created is the caller's active tenant, and
        // it is reached without the caller ever entering credentials again.
        TestsHelper.SetAuthToken(client, onboarded.Session.AccessToken);

        var (infoResponse, info) = await client.GETAsync<GetInfoEndpoint, UserGetInfoResponse>();

        infoResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        info.ActiveTenantId.Should().Be(onboarded.Id, "the session was re-established around the tenant just created");
        info.Tenants.Select(tenant => tenant.Id).Should().Equal([onboarded.Id]);
    }

    /// <summary>
    /// Verifies that self-service creation is held to the same naming rules, the same duplicate
    /// comparison and the same error codes and field names as a tenant created from the platform: an
    /// identifier already taken is refused against the identifier field with the same code, and the
    /// same invalid values come back naming the same fields - while nothing is written either way
    /// (AC-134).
    /// </summary>
    [Fact]
    public async Task Applies_The_Same_Rules_As_Platform_Create()
    {
        var creator = await CreateAccountWithoutMembershipAsync();
        var client = await ClientForAsync(creator.Username);
        var existing = await CreateTenantAsync();

        var (duplicate, duplicateProblem) = await client
            .POSTAsync<TenantOnboardEndpoint, TenantOnboardRequest, ProblemDetails>(
                new() { Name = "Onboarded Tenant", Identifier = existing.Identifier });

        // The very same values the platform surface is given below, so the two answers can be compared
        // rather than each being asserted against a copy of the rules.
        var invalid = new TenantOnboardRequest { Name = string.Empty, Identifier = "ab" };

        var (refused, refusedProblem) = await client
            .POSTAsync<TenantOnboardEndpoint, TenantOnboardRequest, ProblemDetails>(invalid);

        await SetPlatformAdminAuthTokenAsync();

        var (platformDuplicate, platformDuplicateProblem) = await App.Client
            .POSTAsync<TenantCreateEndpoint, TenantCreateRequest, ProblemDetails>(
                new() { Name = "Platform Tenant", Identifier = existing.Identifier });

        var (platformRefused, platformRefusedProblem) = await App.Client
            .POSTAsync<TenantCreateEndpoint, TenantCreateRequest, ProblemDetails>(
                new() { Name = invalid.Name, Identifier = invalid.Identifier });

        duplicate.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        duplicateProblem.Errors.Should().ContainSingle();
        duplicateProblem.Errors.First().Name.Should().Be("identifier", "the caller is told which value was refused");
        duplicateProblem.Errors.First().Code.Should().Be(ErrorCodes.TenantIdentifierAlreadyExists);

        platformDuplicate.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        platformDuplicateProblem.Errors.First().Code.Should().Be(
            duplicateProblem.Errors.First().Code,
            "one comparison answers both surfaces, so an identifier taken from one is taken from the other");

        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        platformRefused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        refusedProblem.Errors.Select(error => error.Name).Should().Equal(
            [.. platformRefusedProblem.Errors.Select(error => error.Name)],
            "both fields broke a rule, and each failure names the field it belongs to - the same rule set answers both surfaces");

        (await MembershipRowsAsync(existing.Id, creator.Id)).Should().BeEmpty("a refused onboarding writes no membership in the tenant it collided with");
    }

    /// <summary>
    /// Verifies that creating a tenant confers authority inside the tenant created and nowhere else:
    /// the onboarded caller is refused a platform-tier operation on another tenant, is admitted the
    /// tenant-tier work of its own, and the same operation on the same other tenant is answered for a
    /// platform administrator - so the refusal is about the caller's standing rather than about the
    /// request (AC-135, AC-146).
    /// </summary>
    [Fact]
    public async Task Grants_No_Platform_Permission()
    {
        var creator = await CreateAccountWithoutMembershipAsync();
        var client = await ClientForAsync(creator.Username);
        var foreign = await CreateTenantAsync();

        var (onboardResponse, onboarded) = await OnboardAsync(client);

        onboardResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        TestsHelper.SetAuthToken(client, onboarded.Session.AccessToken);

        var (refused, _) = await client
            .POSTAsync<TenantSuspendEndpoint, TenantSuspendRequest, TenantSuspendResponse>(new() { Id = foreign.Id });

        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden, "administering the tenant created confers nothing over any other tenant");

        // The tenant-tier work the same token is admitted to, so the refusal above is the platform tier
        // and not the endpoint being out of reach for this caller altogether.
        var (admitted, _) = await client
            .GETAsync<TenantMemberListEndpoint, TenantMemberListRequest, TenantMemberListResponse>(
                new() { TenantId = onboarded.Id, All = true });

        admitted.StatusCode.Should().Be(HttpStatusCode.OK, "the creator administers the tenant it created, and reading its members is tenant-tier work");

        // The control: the very same request, against the very same tenant, answered for the caller
        // that does hold platform administration.
        await SetPlatformAdminAuthTokenAsync();

        var (suspended, _) = await App.Client
            .POSTAsync<TenantSuspendEndpoint, TenantSuspendRequest, TenantSuspendResponse>(new() { Id = foreign.Id });

        suspended.StatusCode.Should().Be(HttpStatusCode.OK, "so the 403 above is the onboarded caller's authority, not the tenant or the operation");
    }

    /// <summary>
    /// Verifies that an unauthenticated caller is refused and that nothing is written for them, because
    /// onboarding attaches a tenant to an account that exists rather than bringing one into being
    /// (AC-136).
    /// </summary>
    [Fact]
    public async Task Unauthenticated()
    {
        ClearAuthToken();

        var identifier = NewTenantIdentifier();

        var (response, refusal) = await App.Client
            .POSTAsync<TenantOnboardEndpoint, TenantOnboardRequest, ProblemDetails>(
                new() { Name = "Onboarded Tenant", Identifier = identifier });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        refusal.Errors.First().Code.Should().Be(ErrorCodes.AuthenticationRequired,
            "the refusal names the account the operation requires rather than answering with a bare 401 (AC-136)");

        var written = await DbContext.Tenants
            .AcrossAllTenants()
            .IgnoreQueryFilters([SoftDeleteFilterKey])
            .AsNoTracking()
            .AnyAsync(tenant => tenant.IdentifierNormalized == identifier, TestContext.Current.CancellationToken);

        written.Should().BeFalse("a refused request writes no tenant at all");
    }

    /// <summary>
    /// Verifies that an account already belonging to a tenant may create another one, and that the
    /// tenant it creates records that account as its creator - so onboarding is open to every
    /// authenticated account rather than only to one with no tenant (AC-137).
    /// </summary>
    [Fact]
    public async Task Existing_Member_Can_Create_Another_Tenant()
    {
        var existing = await CreateTenantAsync();
        var member = await CreateTenantUserAsync(existing.Id);
        var client = await ClientForAsync(member.Username, existing.Id);

        var (response, onboarded) = await OnboardAsync(client);

        response.StatusCode.Should().Be(HttpStatusCode.OK, "how many tenants the caller already belongs to is never asked");

        var cancellationToken = TestContext.Current.CancellationToken;

        (await MembershipService.IsMemberAsync(onboarded.Id, member.Id, cancellationToken))
            .Should().BeTrue("the creator is a member of the tenant it created");
        (await MembershipService.IsMemberAsync(existing.Id, member.Id, cancellationToken))
            .Should().BeTrue("and its earlier membership is untouched by the tenant it went on to create");

        var created = await DbContext.Tenants
            .AcrossAllTenants()
            .AsNoTracking()
            .SingleAsync(tenant => tenant.Id == onboarded.Id, cancellationToken);

        created.CreatedBy.Should().Be(member.Id, "the account that created the tenant is recorded on it");
    }

    /// <summary>
    /// Verifies that onboarding makes the creator the administrator of the tenant created and leaves
    /// every other tenant alone: its authority does not reach another tenant's membership, and no
    /// membership anywhere else is written (AC-146).
    /// </summary>
    [Fact]
    public async Task Creator_Is_Administrator_Only_Of_The_Tenant_Created()
    {
        var creator = await CreateAccountWithoutMembershipAsync();
        var client = await ClientForAsync(creator.Username);
        var foreign = await CreateTenantAsync();

        // Read before the onboarding, through the platform surface, so the comparison afterwards is
        // against what the other tenant looked like rather than against an assumption about it.
        await SetPlatformAdminAuthTokenAsync();
        var foreignMembersBefore = await LiveMemberIdsAsync(foreign.Id);

        var (onboardResponse, onboarded) = await OnboardAsync(client);

        onboardResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        TestsHelper.SetAuthToken(client, onboarded.Session.AccessToken);

        var administratorRoleId = await TenantAdministratorRoleIdAsync(onboarded.Id);

        (await GrantedRolesAsync(onboarded.Id, creator.Id))
            .Should().Equal([administratorRoleId], "the creator is an administrator of the tenant created");

        var (refused, problem) = await client
            .POSTAsync<TenantMemberAddEndpoint, TenantMemberAddRequest, ProblemDetails>(
                new() { TenantId = foreign.Id, UserId = creator.Id, Roles = [] });

        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.Should().ContainSingle();
        problem.Errors.First().Code.Should().Be(ErrorCodes.NotTenantMember, "the authority onboarding confers stops at the boundary of the tenant created");

        (await MembershipRowsAsync(foreign.Id, creator.Id)).Should().BeEmpty("no membership of another tenant was written");
        (await LiveMemberIdsAsync(foreign.Id)).Should().BeEquivalentTo(foreignMembersBefore, "and no other tenant's membership was changed");
    }

    /// <summary>
    /// Creates a tenant for a caller through the endpoint under test, naming a tenant and an identifier
    /// no other test can hold.
    /// </summary>
    /// <param name="client">The client whose identity is to onboard.</param>
    /// <returns>The endpoint's answer, and the tenant it reports.</returns>
    private static Task<TestResult<TenantOnboardResponse>> OnboardAsync(HttpClient client)
        => client.POSTAsync<TenantOnboardEndpoint, TenantOnboardRequest, TenantOnboardResponse>(
            new() { Name = "Onboarded Tenant", Identifier = NewTenantIdentifier() });

    /// <summary>
    /// The identifiers of the accounts currently holding a membership of one tenant, read through the
    /// platform surface, so a test can tell a tenant nobody joined from one that was changed.
    /// </summary>
    /// <param name="tenantId">The tenant whose members are read.</param>
    /// <returns>The identifiers of its members.</returns>
    private async Task<List<Guid>> LiveMemberIdsAsync(Guid tenantId)
    {
        var (response, page) = await App.Client
            .GETAsync<TenantMemberListEndpoint, TenantMemberListRequest, TenantMemberListResponse>(
                new() { TenantId = tenantId, All = true });

        response.StatusCode.Should().Be(HttpStatusCode.OK, "a test reading a tenant's members must actually have read them");
        return [.. page.Items.Select(item => item.Id)];
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
    /// can tell a membership that was not written from one that was removed, and read the audit values
    /// a creation was recorded with.
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
