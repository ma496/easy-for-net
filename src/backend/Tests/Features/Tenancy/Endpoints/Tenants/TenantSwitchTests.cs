namespace Backend.Tests.Features.Tenancy.Endpoints.Tenants;

using Backend.ShareData.Entities;
using Backend.Features.Identity.Core.Entities;
using Backend.Features.Identity.Endpoints.Account;
using Backend.Features.Identity.Endpoints.Roles;
using Backend.Features.Tenancy.Core.Entities;
using Backend.Features.Tenancy.Core;
using Backend.Features.Tenancy.Endpoints.Tenants;

/// <summary>
/// Tests for <see cref="TenantSwitchEndpoint"/>: selecting the tenant a session acts in, what the
/// selection does to the requests that follow it, the three states it refuses and the one answer it
/// gives for all of them, the tenant a request cannot supply for itself, and the account that holds
/// several memberships and therefore starts with none (AC-025, AC-026, AC-027, AC-108, AC-109, AC-123,
/// AC-138, AC-139, AC-140, AC-148, AC-149).
/// </summary>
/// <remarks>
/// <para>
/// The tenant-scoped surface used throughout is the role list, because it is the one place a tenant's
/// own rows are named without any tenant having to be addressed: it reads the roles of the tenant the
/// session names, so a test can create a role in each of two tenants and tell from the answer alone
/// which tenant the request acted in.
/// </para>
/// <para>
/// Every account here is made by the test and joined to the tenants the test created. The seeded
/// accounts are read in exactly one place - the <c>dual</c> account, whose two memberships are the
/// only seeded state that produces a sign-in with no active tenant - and nothing here writes to a
/// seeded tenant or to a seeded account's memberships.
/// </para>
/// </remarks>
public class TenantSwitchTests(App app) : TenancyTestsBase(app)
{
    /// <summary>
    /// The soft-delete query filter's registered key, named so a read that has to see a retained row
    /// can relax that one filter and leave every other in force.
    /// </summary>
    private const string SoftDeleteFilterKey = "SoftDelete";

    /// <summary>
    /// The header a client might think carries a tenant. Nothing reads it: the tenant a request acts in
    /// travels in the session, so naming a tenant here is naming one the request does not get to
    /// choose - which is what the test below establishes by sending it.
    /// </summary>
    private const string SuppliedTenantHeader = "X-Tenant-Id";

    /// <summary>
    /// Verifies that switching re-establishes the caller's session around the tenant selected while
    /// leaving them authenticated: the answer names that tenant and carries a freshly issued token pair
    /// whose persisted row records the same tenant, the request it was asked with carries nothing but a
    /// tenant and no credential of any kind, and no credentials are re-entered anywhere along the way
    /// (AC-139).
    /// </summary>
    [Fact]
    public async Task Valid_Input()
    {
        var (tenantA, _) = await PrepareTenantAsync();
        var (tenantB, _) = await PrepareTenantAsync();
        var roleInA = await CreateTenantRoleAsync(tenantA.Id, Allow.Role_View);
        var roleInB = await CreateTenantRoleAsync(tenantB.Id, Allow.Role_View);
        var user = await CreateAccountWithoutMembershipAsync();

        await JoinAsync(tenantA.Id, user.Id, roleInA);
        await JoinAsync(tenantB.Id, user.Id, roleInB);

        var client = await ClientForAsync(user.Username, tenantA.Id);
        var presented = client.DefaultRequestHeaders.Authorization!.Parameter;

        var (response, switched) = await client
            .POSTAsync<TenantSwitchEndpoint, TenantSwitchRequest, TenantSwitchResponse>(new() { TenantId = tenantB.Id });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        switched.TenantId.Should().Be(tenantB.Id);
        switched.Name.Should().Be(tenantB.Name);
        switched.Identifier.Should().Be(tenantB.Identifier);
        switched.Status.Should().Be(TenantStatus.Active);

        switched.Session.UserId.Should().Be(user.Id, "the same account carries on, in another tenant");
        switched.Session.AccessToken.Should().NotBeNullOrWhiteSpace();
        switched.Session.AccessToken.Should().NotBe(presented, "the session was re-established, so a pair this session never held is what carries it");

        // The tenant is not a value a caller may send alongside a credential: the payload names the
        // selection and nothing else, so there is no field in it a token or a tenant could be smuggled
        // through.
        var payload = typeof(TenantSwitchRequest).GetProperties();
        payload.Should().ContainSingle("a switch request names a tenant and carries no credential of any kind");
        payload[0].Name.Should().Be(nameof(TenantSwitchRequest.TenantId));
        payload[0].PropertyType.Should().Be(typeof(Guid));

        var sessions = await DbContext.AuthTokens
            .AsNoTracking()
            .AcrossAllTenants()
            .Where(row => row.UserId == user.Id)
            .OrderByDescending(row => row.CreatedAt)
            .ToListAsync(TestContext.Current.CancellationToken);

        sessions.Should().NotBeEmpty("every session established for the account is recorded");
        sessions[0].TenantId.Should().Be(tenantB.Id, "the newest row is the session the switch established, and the row a later refresh reads is what carries the selection forward");
        sessions.Should().Contain(row => row.TenantId == tenantA.Id,
            "while the session it replaced is still recorded against the tenant it was established for - this client carries no cookie, so it presents no refresh token for the switch to revoke; the client that does is covered below");
    }

    /// <summary>
    /// Verifies that the selection applies to every request that follows it without a second sign-in:
    /// the same client, presenting the same account's session, reads the newly selected tenant's rows
    /// and none of the previously selected tenant's (AC-025).
    /// </summary>
    [Fact]
    public async Task Switch_Applies_To_Subsequent_Requests()
    {
        var (tenantA, _) = await PrepareTenantAsync();
        var (tenantB, _) = await PrepareTenantAsync();
        var roleInA = await CreateTenantRoleAsync(tenantA.Id, Allow.Role_View);
        var roleInB = await CreateTenantRoleAsync(tenantB.Id, Allow.Role_View);
        var user = await CreateAccountWithoutMembershipAsync();

        await JoinAsync(tenantA.Id, user.Id, roleInA);
        await JoinAsync(tenantB.Id, user.Id, roleInB);

        var client = await ClientForAsync(user.Username, tenantA.Id);
        var roleNamesInA = await ReadRoleNameAsync(roleInA);
        var roleNamesInB = await ReadRoleNameAsync(roleInB);

        var (before, visibleBefore) = await VisibleRoleNamesAsync(client);

        before.StatusCode.Should().Be(HttpStatusCode.OK);
        visibleBefore.Should().Contain(roleNamesInA).And.NotContain(roleNamesInB);

        await TestsHelper.SwitchTenantAsync(client, tenantB.Id);

        var (after, visibleAfter) = await VisibleRoleNamesAsync(client);

        after.StatusCode.Should().Be(HttpStatusCode.OK, "the same session, in the tenant it selected, asks again without signing in");
        visibleAfter.Should().Contain(roleNamesInB).And.NotContain(roleNamesInA, "the rows of the tenant acted in a moment ago are no longer within reach");
    }

    /// <summary>
    /// Verifies that what a caller may do comes from the tenant they are acting in: an account holding
    /// only tenant viewing in one tenant is refused its role creation, and the same call succeeds the
    /// moment it switches to the tenant where it is an administrator - with nothing having changed but
    /// the selection (AC-027).
    /// </summary>
    [Fact]
    public async Task Permissions_Come_Only_From_The_Active_Tenant()
    {
        var (tenantA, _) = await PrepareTenantAsync();
        var (tenantB, administratorRoleOfB) = await PrepareTenantAsync();
        var viewingOnly = await CreateTenantRoleAsync(tenantA.Id, Allow.Tenant_View);
        var user = await CreateAccountWithoutMembershipAsync();

        // Limited in one tenant, administrator in the other, so the same caller's authority differs
        // between them and the difference is the selection rather than the account.
        await JoinAsync(tenantA.Id, user.Id, viewingOnly);
        await JoinAsync(tenantB.Id, user.Id, administratorRoleOfB);

        var client = await ClientForAsync(user.Username, tenantA.Id);

        var (refused, _) = await client
            .POSTAsync<RoleCreateEndpoint, RoleCreateRequest, RoleCreateResponse>(NewRole());

        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden, "holding administration elsewhere is not authority here");

        await TestsHelper.SwitchTenantAsync(client, tenantB.Id);

        var (admitted, created) = await client
            .POSTAsync<RoleCreateEndpoint, RoleCreateRequest, RoleCreateResponse>(NewRole());

        admitted.StatusCode.Should().Be(HttpStatusCode.OK, "the same call, by the same caller, in the tenant where it administers");
        created.Name.Should().NotBeNullOrWhiteSpace();
    }

    /// <summary>
    /// Verifies that a caller holding every permission in one tenant is still refused a tenant they
    /// hold no membership in: standing is the membership and not the authority, so switching to such a
    /// tenant reports the caller is not a member rather than granting anything (AC-026).
    /// </summary>
    [Fact]
    public async Task Cannot_Switch_To_Non_Member_Tenant()
    {
        var (ownTenant, _) = await PrepareTenantAsync();
        var (foreignTenant, _) = await PrepareTenantAsync();
        var user = await CreateAccountWithoutMembershipAsync();

        // Its own tenant's administrator role, which carries every permission a tenant can be
        // administered with - and none of them is standing in another tenant.
        var administratorOfOwn = await TenantAdministratorRoleIdAsync(ownTenant.Id);
        await JoinAsync(ownTenant.Id, user.Id, administratorOfOwn);

        var client = await ClientForAsync(user.Username, ownTenant.Id);

        var (refused, problem) = await client
            .POSTAsync<TenantSwitchEndpoint, TenantSwitchRequest, ProblemDetails>(new() { TenantId = foreignTenant.Id });

        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.Should().ContainSingle();
        problem.Errors.First().Code.Should().Be(ErrorCodes.NotTenantMember);
    }

    /// <summary>
    /// Verifies that a platform administrator enters a tenant they hold no membership in, and that
    /// the session they are left with really acts inside it: their platform-scoped role holds in every
    /// tenant, so they carry that tenant's permissions there and read its rows rather than another
    /// tenant's. This is what lets a tenant reporting that something is broken be answered from inside
    /// that tenant.
    /// </summary>
    [Fact]
    public async Task Platform_Administrator_Enters_A_Tenant_They_Do_Not_Belong_To()
    {
        var (tenant, _) = await PrepareTenantAsync();
        var roleInTenant = await CreateTenantRoleAsync(tenant.Id, Allow.Role_View);

        var account = await CreateAccountWithoutMembershipAsync();
        await UserService.AssignRoleAsync(account.Id, TestRoles.PlatformAdminRoleId);
        await MarkAsPlatformAccountAsync(account.Id);

        var client = await ClientForAsync(account.Username);

        var (entered, selection) = await client
            .POSTAsync<TenantSwitchEndpoint, TenantSwitchRequest, TenantSwitchResponse>(new() { TenantId = tenant.Id });

        entered.StatusCode.Should().Be(HttpStatusCode.OK,
            "platform administration belongs to no tenant and so is standing in all of them");
        selection.TenantId.Should().Be(tenant.Id);

        TestsHelper.SetAuthToken(client, selection.Session.AccessToken);

        // The roles of the tenant entered are what comes back, so the session is acting inside it
        // rather than merely naming it.
        var (listed, roles) = await client
            .GETAsync<RoleListEndpoint, RoleListRequest, RoleListResponse>(new() { All = true });

        listed.StatusCode.Should().Be(HttpStatusCode.OK,
            "the platform role holds in every tenant, so its permissions are the ones carried inside this one");
        roles.Items.Select(role => role.Id).Should().Contain(roleInTenant,
            "the request acts in the tenant just entered, so that tenant's own roles are what it reads");

        (await MembershipCountAsync(account.Id)).Should().Be(0,
            "entering a tenant is not joining it: nothing was written to the membership rows");
    }

    /// <summary>
    /// Verifies that a platform administrator is refused a suspended tenant exactly as anyone else is.
    /// Reaching every tenant is not reaching one that is out of service: a suspended tenant is
    /// reactivated before it is worked in, never entered around the suspension.
    /// </summary>
    [Fact]
    public async Task Platform_Administrator_Cannot_Enter_A_Suspended_Tenant()
    {
        var (suspended, _) = await PrepareTenantAsync(TenantStatus.Suspended);

        var account = await CreateAccountWithoutMembershipAsync();
        await UserService.AssignRoleAsync(account.Id, TestRoles.PlatformAdminRoleId);
        await MarkAsPlatformAccountAsync(account.Id);

        var client = await ClientForAsync(account.Username);

        var (refused, problem) = await client
            .POSTAsync<TenantSwitchEndpoint, TenantSwitchRequest, ProblemDetails>(new() { TenantId = suspended.Id });

        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.Should().ContainSingle();
        problem.Errors.First().Code.Should().Be(ErrorCodes.TenantSuspended);
    }

    /// <summary>
    /// Verifies that a platform administrator who has entered a tenant can leave it and is put back
    /// into platform scope, and that an ordinary member is not offered the same way out. Leaving is
    /// what keeps entering from being one-way: a platform administrator holds no membership anywhere,
    /// so there is no other tenant of theirs to select their way out through.
    /// </summary>
    [Fact]
    public async Task Platform_Administrator_Leaves_A_Tenant_Through_Exit()
    {
        var (tenant, _) = await PrepareTenantAsync();

        var account = await CreateAccountWithoutMembershipAsync();
        await UserService.AssignRoleAsync(account.Id, TestRoles.PlatformAdminRoleId);
        await MarkAsPlatformAccountAsync(account.Id);

        var client = await ClientForAsync(account.Username);
        await TestsHelper.SwitchTenantAsync(client, tenant.Id);

        var (inside, insideInfo) = await client.GETAsync<GetInfoEndpoint, UserGetInfoResponse>();

        inside.StatusCode.Should().Be(HttpStatusCode.OK);
        insideInfo.ActiveTenantId.Should().Be(tenant.Id,
            "a platform administrator is told which tenant they are working in, though they are a member of none");

        var (left, exit) = await client.POSTAsync<TenantExitEndpoint, TenantExitResponse>();

        left.StatusCode.Should().Be(HttpStatusCode.OK);
        TestsHelper.SetAuthToken(client, exit.Session.AccessToken);

        var (outside, outsideInfo) = await client.GETAsync<GetInfoEndpoint, UserGetInfoResponse>();

        outside.StatusCode.Should().Be(HttpStatusCode.OK);
        outsideInfo.ActiveTenantId.Should().BeNull("the session was re-established naming no tenant at all");
        outsideInfo.IsPlatform.Should().BeTrue("leaving a tenant does not touch what the account is");
    }

    /// <summary>
    /// Verifies that leaving is closed to a caller who is not a platform administrator. For a member
    /// acting in no tenant is not a place to work but a state to leave, so it is not offered to them
    /// and then regretted.
    /// </summary>
    [Fact]
    public async Task Ordinary_Member_Cannot_Exit_To_Platform_Scope()
    {
        var (tenant, _) = await PrepareTenantAsync();
        var member = await CreateTenantUserAsync(tenant.Id);

        var client = await ClientForAsync(member.Username, tenant.Id);

        var (refused, _) = await client.POSTAsync<TenantExitEndpoint, TenantExitResponse>();

        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "the way out of a tenant for a member is selecting another one, not acting in none");
    }

    /// <summary>
    /// Verifies that a withdrawn membership costs the caller the tenant at their session's next
    /// renewal: the renewal succeeds and hands back a session naming no tenant, so the request that
    /// the membership admitted a moment earlier is refused for the authority the session no longer
    /// carries (AC-109).
    /// </summary>
    [Fact]
    public async Task Withdrawn_Membership_Is_Dropped_At_The_Next_Renewal()
    {
        var (tenant, _) = await PrepareTenantAsync();
        var roleHoldingView = await CreateTenantRoleAsync(tenant.Id, Allow.Role_View);
        var user = await CreateAccountWithoutMembershipAsync();

        await JoinAsync(tenant.Id, user.Id, roleHoldingView);

        var session = await SessionForAsync(user.Username, tenant.Id);

        var (admitted, _) = await session.Client
            .GETAsync<RoleListEndpoint, RoleListRequest, RoleListResponse>(new() { All = true });

        admitted.StatusCode.Should().Be(HttpStatusCode.OK, "the role the membership granted is what admits this call");

        var membershipRevoked = await MembershipService.RemoveAsync(tenant.Id, user.Id, TestContext.Current.CancellationToken);

        membershipRevoked.Should().Be(TenantMembershipChangeOutcome.Applied);

        await session.RenewAsync();

        var (refused, problem) = await session.Client
            .GETAsync<RoleListEndpoint, RoleListRequest, ProblemDetails>(new() { All = true });

        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        refused.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized, "the caller is still authenticated and is offered another tenant rather than signed out");
        problem.Errors.Should().ContainSingle();
        problem.Errors.First().Code.Should().Be(ErrorCodes.PermissionDenied,
            "the renewed session names no tenant, so it holds nothing the endpoint requires");
    }

    /// <summary>
    /// Verifies that the tenant a request acts in cannot be supplied by that request: with the session
    /// acting in one tenant, a tenant named in the payload and one named in a header are both inert,
    /// and changing the supplied value changes nothing about what is read (AC-138).
    /// </summary>
    [Fact]
    public async Task Request_Supplied_Tenant_Is_Ignored()
    {
        var (tenantA, _) = await PrepareTenantAsync();
        var (tenantB, _) = await PrepareTenantAsync();
        var roleInA = await CreateTenantRoleAsync(tenantA.Id, Allow.Role_View);
        var roleInB = await CreateTenantRoleAsync(tenantB.Id, Allow.Role_View);
        var user = await CreateAccountWithoutMembershipAsync();

        await JoinAsync(tenantA.Id, user.Id, roleInA);
        await JoinAsync(tenantB.Id, user.Id, roleInB);

        var client = await ClientForAsync(user.Username, tenantA.Id);
        var roleNamesInA = await ReadRoleNameAsync(roleInA);
        var roleNamesInB = await ReadRoleNameAsync(roleInB);

        client.DefaultRequestHeaders.Add(SuppliedTenantHeader, tenantB.Id.ToString());

        var (namingB, visibleWhileNamingB) = await VisibleRoleNamesAsync(client, tenantB.Id);
        var (namingNone, visibleWhileNamingNone) = await VisibleRoleNamesAsync(client);

        namingB.StatusCode.Should().Be(HttpStatusCode.OK);
        visibleWhileNamingB.Should().Contain(roleNamesInA).And.NotContain(roleNamesInB, "a tenant named by the request is not the tenant the request acts in");

        // The same request with the value changed, so the comparison is between two readings rather
        // than between a reading and an expectation about it.
        namingNone.StatusCode.Should().Be(HttpStatusCode.OK);
        visibleWhileNamingNone.Should().Equal(visibleWhileNamingB, "changing the tenant the request names changes nothing about what it reads");
    }

    /// <summary>
    /// Verifies both halves of the rule at once: a tenant named by the request is ignored while the
    /// session is good, and the tenant the session names is what governs the request afterwards - so a
    /// session that has lost its tenant is refused whatever the request itself names, even when it
    /// names a tenant the caller does still belong to (AC-148).
    /// </summary>
    [Fact]
    public async Task Session_Tenant_Governs_The_Request()
    {
        var (tenantA, _) = await PrepareTenantAsync();
        var (tenantB, _) = await PrepareTenantAsync();
        var roleInA = await CreateTenantRoleAsync(tenantA.Id, Allow.Role_View);
        var roleInB = await CreateTenantRoleAsync(tenantB.Id, Allow.Role_View);
        var user = await CreateAccountWithoutMembershipAsync();

        await JoinAsync(tenantA.Id, user.Id, roleInA);
        await JoinAsync(tenantB.Id, user.Id, roleInB);

        var session = await SessionForAsync(user.Username, tenantA.Id);

        session.Client.DefaultRequestHeaders.Add(SuppliedTenantHeader, tenantB.Id.ToString());

        var (before, visibleBefore) = await VisibleRoleNamesAsync(session.Client, tenantB.Id);

        before.StatusCode.Should().Be(HttpStatusCode.OK);
        visibleBefore.Should().Contain(await ReadRoleNameAsync(roleInA)).And.NotContain(await ReadRoleNameAsync(roleInB));

        // The session still names tenant A, and the membership it was established on is withdrawn. The
        // request keeps naming tenant B, which the caller is still a member of - and that is exactly
        // what does not help it: the session is what governs, so the renewal leaves it with no tenant
        // rather than moving it to the one the request asked for.
        (await MembershipService.RemoveAsync(tenantA.Id, user.Id, TestContext.Current.CancellationToken))
            .Should().Be(TenantMembershipChangeOutcome.Applied);

        await session.RenewAsync();

        var (refused, problem) = await session.Client
            .GETAsync<RoleListEndpoint, RoleListRequest, ProblemDetails>(new() { TenantId = tenantB.Id, All = true });

        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        problem.Errors.Should().ContainSingle();
        problem.Errors.First().Code.Should().Be(ErrorCodes.PermissionDenied);
    }

    /// <summary>
    /// Verifies that an account holding more than one membership is asked which tenant it means rather
    /// than signed in without one: nothing is chosen on its behalf, and nothing signs it in to a
    /// session in which none of its memberships apply (AC-140).
    /// </summary>
    [Fact]
    public async Task Several_Memberships_Must_Name_A_Tenant()
    {
        var visitor = App.CreateClient(new ClientOptions { HandleCookies = false });

        var (refused, problem) = await visitor
            .POSTAsync<TokenEndpoint, TokenRequest, ProblemDetails>(
                new() { Username = "dual", Password = TestUsers.DefaultPassword });

        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "nothing is chosen on the caller's behalf when there is a choice to make");
        problem.Errors.Should().ContainSingle();
        problem.Errors.First().Code.Should().Be(ErrorCodes.TenantRequired);

        // Naming one is what the refusal asks for, and the answer then reports the whole choice - so
        // the caller can switch to the other without signing in again.
        var client = await ClientForAsync("dual", TestTenants.SecondTenantId);

        var (infoResponse, info) = await client.GETAsync<GetInfoEndpoint, UserGetInfoResponse>();

        infoResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        info.ActiveTenantId.Should().Be(TestTenants.SecondTenantId);
        info.Tenants.Should().HaveCountGreaterThanOrEqualTo(2, "the memberships it holds are all available to choose from");
    }

    /// <summary>
    /// Verifies that selecting one of several tenants grants access to exactly that tenant's data: the
    /// caller starts in the other tenant, switches, and is then answered with the chosen tenant's row
    /// and not the one it left (AC-149).
    /// </summary>
    [Fact]
    public async Task Selection_Grants_Exactly_That_Tenants_Data()
    {
        var (tenantA, _) = await PrepareTenantAsync();
        var (tenantB, _) = await PrepareTenantAsync();
        var roleInA = await CreateTenantRoleAsync(tenantA.Id, Allow.Role_View);
        var roleInB = await CreateTenantRoleAsync(tenantB.Id, Allow.Role_View);
        var user = await CreateAccountWithoutMembershipAsync();

        await JoinAsync(tenantA.Id, user.Id, roleInA);
        await JoinAsync(tenantB.Id, user.Id, roleInB);

        // The caller starts in tenant B, so what the switch below has to produce is the other tenant's
        // data rather than merely some data.
        var client = await ClientForAsync(user.Username, tenantB.Id);

        var (started, visibleAtStart) = await VisibleRoleNamesAsync(client);

        started.StatusCode.Should().Be(HttpStatusCode.OK);
        visibleAtStart.Should().Contain(await ReadRoleNameAsync(roleInB));

        await TestsHelper.SwitchTenantAsync(client, tenantA.Id);

        var (admitted, visible) = await VisibleRoleNamesAsync(client);

        admitted.StatusCode.Should().Be(HttpStatusCode.OK);
        visible.Should().Contain(await ReadRoleNameAsync(roleInA));
        visible.Should().NotContain(await ReadRoleNameAsync(roleInB), "selecting one tenant is not a licence to read another");
    }

    /// <summary>
    /// Verifies that an account holding exactly one membership is never asked to choose: sign-in
    /// resolves that tenant on its own, it is the session's active tenant, and a tenant-scoped call
    /// succeeds with no switch having been performed (AC-123).
    /// </summary>
    [Fact]
    public async Task Single_Membership_Is_Auto_Selected()
    {
        var (tenant, _) = await PrepareTenantAsync();
        var roleHoldingView = await CreateTenantRoleAsync(tenant.Id, Allow.Role_View);
        var user = await CreateAccountWithoutMembershipAsync();

        await JoinAsync(tenant.Id, user.Id, roleHoldingView);

        var client = await ClientForAsync(user.Username);

        var (infoResponse, info) = await client.GETAsync<GetInfoEndpoint, UserGetInfoResponse>();

        infoResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        info.ActiveTenantId.Should().Be(tenant.Id, "there was nothing to choose between, so the caller is put to work rather than asked");
        info.ActiveTenant.Should().NotBeNull();
        info.ActiveTenant!.Id.Should().Be(tenant.Id);

        var (admitted, visible) = await VisibleRoleNamesAsync(client);

        admitted.StatusCode.Should().Be(HttpStatusCode.OK, "no selection is required for a caller who holds one membership");
        visible.Should().Contain(await ReadRoleNameAsync(roleHoldingView));
    }

    /// <summary>
    /// Verifies that a tenant that has been deleted is refused exactly as a tenant that never existed
    /// is: the same status, the same code and the same message, so a stored selection naming a tenant
    /// that is gone discloses nothing about whether that tenant was ever real (AC-086).
    /// </summary>
    [Fact]
    public async Task Cannot_Switch_To_Deleted_Tenant()
    {
        var (liveTenant, _) = await PrepareTenantAsync();
        var (retiredTenant, _) = await PrepareTenantAsync();
        var user = await CreateAccountWithoutMembershipAsync();

        await JoinAsync(liveTenant.Id, user.Id, await CreateTenantRoleAsync(liveTenant.Id));
        await JoinAsync(retiredTenant.Id, user.Id, await CreateTenantRoleAsync(retiredTenant.Id));

        await RetireTenantAsync(retiredTenant.Id);

        var client = await ClientForAsync(user.Username, liveTenant.Id);

        var (refusedRetired, retiredProblem) = await client
            .POSTAsync<TenantSwitchEndpoint, TenantSwitchRequest, ProblemDetails>(new() { TenantId = retiredTenant.Id });

        var (refusedUnknown, unknownProblem) = await client
            .POSTAsync<TenantSwitchEndpoint, TenantSwitchRequest, ProblemDetails>(new() { TenantId = Guid.NewGuid() });

        refusedRetired.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        refusedUnknown.StatusCode.Should().Be(refusedRetired.StatusCode);
        retiredProblem.Errors.First().Code.Should().Be(ErrorCodes.TenantNotFound);
        unknownProblem.Errors.First().Code.Should().Be(retiredProblem.Errors.First().Code);
        unknownProblem.Errors.First().Reason.Should().Be(retiredProblem.Errors.First().Reason, "a tenant that is gone and one that never was are one answer");
    }

    /// <summary>
    /// Verifies that a suspended tenant cannot be selected: it is out of service rather than gone, so
    /// the refusal names suspension, and the caller keeps the session and the tenant it was acting in
    /// (AC-086).
    /// </summary>
    [Fact]
    public async Task Cannot_Switch_To_Suspended_Tenant()
    {
        var (liveTenant, _) = await PrepareTenantAsync();
        var (suspendedTenant, _) = await PrepareTenantAsync(TenantStatus.Suspended);
        var user = await CreateAccountWithoutMembershipAsync();

        await JoinAsync(liveTenant.Id, user.Id, await CreateTenantRoleAsync(liveTenant.Id, Allow.Role_View));
        await JoinAsync(suspendedTenant.Id, user.Id, await CreateTenantRoleAsync(suspendedTenant.Id, Allow.Role_View));

        var client = await ClientForAsync(user.Username, liveTenant.Id);

        var (refused, problem) = await client
            .POSTAsync<TenantSwitchEndpoint, TenantSwitchRequest, ProblemDetails>(new() { TenantId = suspendedTenant.Id });

        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.Should().ContainSingle();
        problem.Errors.First().Code.Should().Be(ErrorCodes.TenantSuspended, "the tenant exists and the caller belongs to it; it is simply not in service");

        // The session and the tenant it was acting in are untouched, which is what leaves the caller
        // somewhere to carry on working.
        var (stillWorking, _) = await VisibleRoleNamesAsync(client);

        stillWorking.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Verifies that a selection naming no tenant is refused as a validation failure against the tenant
    /// field, before any tenant is looked up (AC-086).
    /// </summary>
    [Fact]
    public async Task Missing_Tenant_Is_Rejected()
    {
        var (tenant, _) = await PrepareTenantAsync();
        var user = await CreateAccountWithoutMembershipAsync();

        await JoinAsync(tenant.Id, user.Id, await CreateTenantRoleAsync(tenant.Id));

        var client = await ClientForAsync(user.Username, tenant.Id);

        var (response, problem) = await client
            .POSTAsync<TenantSwitchEndpoint, TenantSwitchRequest, ProblemDetails>(new() { TenantId = Guid.Empty });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.Select(error => error.Name).Should().Equal(["tenantId"]);
    }

    /// <summary>
    /// Verifies that selecting a tenant requires a session to select it for: an unauthenticated caller
    /// is refused before any tenant is read (AC-086).
    /// </summary>
    [Fact]
    public async Task Unauthenticated()
    {
        ClearAuthToken();

        var (response, _) = await Client
            .POSTAsync<TenantSwitchEndpoint, TenantSwitchRequest, TenantSwitchResponse>(new() { TenantId = Guid.NewGuid() });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Creates a tenant that already has a member - its administrator - and returns it with that role's
    /// identity, so a member added afterwards holds exactly the roles it is given rather than being
    /// granted administration for being the first to arrive.
    /// </summary>
    /// <param name="status">The state to leave the tenant in, for the tests that select one that is not in service.</param>
    /// <returns>The created tenant, and the identity of its system-created administrator role.</returns>
    /// <summary>
    /// How many live memberships an account holds, so a test can state that entering a tenant wrote
    /// none rather than merely that the call succeeded.
    /// </summary>
    /// <param name="userId">The account whose memberships are counted.</param>
    /// <returns>The number of memberships the account holds.</returns>
    private async Task<int> MembershipCountAsync(Guid userId)
        => await DbContext.TenantMemberships
            .AcrossAllTenants()
            .AsNoTracking()
            .CountAsync(membership => membership.UserId == userId, TestContext.Current.CancellationToken);

    private async Task<(Tenant Tenant, Guid AdministratorRoleId)> PrepareTenantAsync(TenantStatus status = TenantStatus.Active)
    {
        var tenant = await CreateTenantAsync(status);
        var administratorRoleId = await TenantAdministratorRoleIdAsync(tenant.Id);

        var firstMember = await CreateAccountWithoutMembershipAsync();
        var added = await MembershipService.AddAsync(tenant.Id, firstMember.Id, [], TestContext.Current.CancellationToken);

        added.RoleIds.Should().Equal([administratorRoleId], "a tenant's first member is its administrator, which is what keeps later members' granted sets exact");

        return (tenant, administratorRoleId);
    }

    /// <summary>
    /// Makes an account a member of a tenant holding exactly the roles named, through the same service
    /// the membership surface uses, and asserts the set was granted as asked.
    /// </summary>
    /// <param name="tenantId">The tenant the account is joining.</param>
    /// <param name="userId">The account joining it.</param>
    /// <param name="roleIds">The roles it is to hold there, and no others.</param>
    private async Task JoinAsync(Guid tenantId, Guid userId, params Guid[] roleIds)
    {
        var added = await MembershipService.AddAsync(tenantId, userId, roleIds, TestContext.Current.CancellationToken);

        added.RoleIds.Should().BeEquivalentTo(roleIds, "an account added to a tenant holding a member already holds the roles it was given and nothing else");
    }

    /// <summary>
    /// Reads the roles one client can see through the role list endpoint, which reads the tenant the
    /// session names and nothing the request itself supplies.
    /// </summary>
    /// <param name="client">The client whose session decides which tenant is read.</param>
    /// <param name="suppliedTenantId">A tenant the request names, which the endpoint is not to honour.</param>
    /// <returns>The endpoint's answer, and the names of the roles it reported.</returns>
    private static async Task<(HttpResponseMessage Response, List<string> Names)> VisibleRoleNamesAsync(
        HttpClient client,
        Guid? suppliedTenantId = null)
    {
        var (response, page) = await client
            .GETAsync<RoleListEndpoint, RoleListRequest, RoleListResponse>(new() { TenantId = suppliedTenantId, All = true });

        response.StatusCode.Should().Be(HttpStatusCode.OK, "a test reading the roles a tenant holds must actually have read them");

        return (response, [.. page.Items.Select(role => role.Name)]);
    }

    /// <summary>
    /// The name a role was created under, read from the database rather than assumed, so a test
    /// asserting that a listing does or does not contain it is asserting about the row that exists.
    /// </summary>
    /// <param name="roleId">The role being read.</param>
    /// <returns>Its name.</returns>
    private async Task<string> ReadRoleNameAsync(Guid roleId)
        => await DbContext.Roles
            .AcrossAllTenants()
            .AsNoTracking()
            .Where(role => role.Id == roleId)
            .Select(role => role.Name)
            .SingleAsync(TestContext.Current.CancellationToken);

    /// <summary>
    /// The identity of a tenant's system-created role - the one holding tenant administration - read
    /// from the role itself rather than from a name, since any tenant may define a role of any name.
    /// </summary>
    /// <param name="tenantId">The tenant whose administrator role is read.</param>
    /// <returns>The identity of that role.</returns>
    private async Task<Guid> TenantAdministratorRoleIdAsync(Guid tenantId)
        => await DbContext.Roles
            .AcrossAllTenants()
            .AsNoTracking()
            .Where(role => role.TenantId == tenantId && role.SystemCreated)
            .Select(role => role.Id)
            .SingleAsync(TestContext.Current.CancellationToken);

    /// <summary>
    /// Retires a tenant the way the platform surface does, so that it reads as absent to every caller
    /// afterwards without the row having been erased.
    /// </summary>
    /// <param name="tenantId">The tenant being retired.</param>
    private async Task RetireTenantAsync(Guid tenantId)
    {
        using var platformScope = TenantContext.BeginPlatformScope();

        var tenant = await DbContext.Tenants
            .IgnoreQueryFilters([SoftDeleteFilterKey])
            .SingleAsync(candidate => candidate.Id == tenantId, TestContext.Current.CancellationToken);

        DbContext.Tenants.Remove(tenant);
        await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// A role creation payload naming a role no other test can hold, with no description so the
    /// optional-description rule has nothing to object to.
    /// </summary>
    /// <returns>The request.</returns>
    private static RoleCreateRequest NewRole() => new() { Name = $"Role {Guid.NewGuid():N}" };

    /// <summary>
    /// Verifies that switching tenant ends the session it replaces rather than leaving it redeemable:
    /// the stored pair the request arrived with is gone afterwards, so the refresh token the caller
    /// held a moment ago cannot be exchanged for a session back in the tenant they left, and the rows
    /// do not accumulate one per switch.
    /// </summary>
    /// <remarks>
    /// The client here carries cookies, which is what a browser does and what
    /// <see cref="TenancyTestsBase.ClientForAsync"/> deliberately does not: the refresh token travels
    /// in a cookie, so only a client that sends it can have the pair it names revoked.
    /// </remarks>
    [Fact]
    public async Task Switching_Revokes_The_Replaced_Sessions_Stored_Pair()
    {
        var (tenantA, _) = await PrepareTenantAsync();
        var (tenantB, _) = await PrepareTenantAsync();
        var user = await CreateAccountWithoutMembershipAsync();
        var cancellationToken = TestContext.Current.CancellationToken;

        await JoinAsync(tenantA.Id, user.Id);
        await JoinAsync(tenantB.Id, user.Id);

        // The account belongs to two tenants, so it signs in to tenant B and switches to tenant A: what
        // is under test is the pair the switch replaces, and naming a tenant is what sign-in requires.
        var client = App.CreateClient();
        var (signedIn, session) = await client.POSTAsync<TokenEndpoint, TokenRequest, TokenResponse>(
            new() { Username = user.Username, Password = TestUsers.DefaultPassword, TenantIdentifier = tenantB.Identifier });

        signedIn.StatusCode.Should().Be(HttpStatusCode.OK);
        TestsHelper.SetAuthToken(client, session.AccessToken);

        var beforeSwitch = await StoredSessionIdsAsync(user.Id);

        beforeSwitch.Should().ContainSingle("signing in stored the one pair the session carries");

        var (response, _) = await client
            .POSTAsync<TenantSwitchEndpoint, TenantSwitchRequest, TenantSwitchResponse>(new() { TenantId = tenantA.Id });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var afterSwitch = await StoredSessionIdsAsync(user.Id);

        afterSwitch.Should().NotIntersectWith(beforeSwitch,
            "the pair the switch replaced is revoked, so the refresh token it was issued with can no longer be redeemed");
        afterSwitch.Should().ContainSingle("the switch leaves one live session rather than one more than it found");

        var stored = await DbContext.AuthTokens
            .AsNoTracking()
            .AcrossAllTenants()
            .Where(row => row.UserId == user.Id)
            .ToListAsync(cancellationToken);

        stored.Should().ContainSingle().Which.TenantId.Should().Be(tenantA.Id,
            "and the session that survives is the one the switch established");
    }

    /// <summary>
    /// The identities of the token pairs stored for one account, which is how a test tells a session
    /// that was replaced from one that is still redeemable.
    /// </summary>
    /// <param name="userId">The account whose stored pairs are read.</param>
    /// <returns>The identifiers of the rows standing for that account.</returns>
    private async Task<List<Guid>> StoredSessionIdsAsync(Guid userId)
        => await DbContext.AuthTokens
            .AsNoTracking()
            .AcrossAllTenants()
            .Where(row => row.UserId == userId)
            .Select(row => row.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
}
