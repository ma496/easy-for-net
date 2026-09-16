namespace Backend.Tests.Features.Tenancy.Core;

using Backend.Features.Identity.Endpoints.Roles;
using Backend.Features.Identity.Endpoints.Users;
using Backend.Features.Notifications.Endpoints.Notifications;

/// <summary>
/// Tests for which tenant a request acts in - exactly one, and never a guess (AC-022, AC-023, AC-050).
/// </summary>
/// <remarks>
/// <para>
/// The three cases here are the three ways the question "which tenant is this request for?" can be
/// answered: a session that has selected one acts in it and in no other, a session that could have
/// selected one but has not is refused rather than served from whichever came first, and a session whose
/// account belongs to no tenant at all is refused with an explanation the client can translate.
/// </para>
/// <para>
/// The seeded accounts are named rather than built, because these are the states only seeding can
/// produce: <c>dual</c> holds an active membership in two tenants - the bootstrap tenant and the second
/// one - which is what a session with no selection looks like, and <c>nomember</c> holds none at all. A
/// test-built account can reach neither state without first breaking the one-membership invariant the
/// rest of the suite signs in on.
/// </para>
/// </remarks>
public class TenantContextResolutionTests(App app) : TenancyTestsBase(app)
{
    /// <summary>
    /// The seeded account holding an active membership in two tenants, and therefore no selected tenant
    /// until it chooses one.
    /// </summary>
    private const string DualMembershipUsername = "dual";

    /// <summary>
    /// The seeded account holding no membership at all.
    /// </summary>
    private const string NoMembershipUsername = "nomember";

    /// <summary>
    /// Verifies that a request made by an account that belongs to two tenants and has selected one acts
    /// in that one alone: the row it creates is attributed to the selected tenant, and no row is written
    /// into the tenant it also belongs to (AC-022).
    /// </summary>
    [Fact]
    public async Task Request_Acts_In_Exactly_One_Tenant()
    {
        // Nothing here arranges a tenant: both are the ones `dual` was seeded into, so what is proved is
        // the selection between them rather than a tenant this test made up.
        var selectedTenantId = TestTenants.SecondTenantId;
        var otherTenantId = TestTenants.BootstrapTenantId;

        var dual = await ClientForAsync(DualMembershipUsername, selectedTenantId);
        var name = $"one-tenancy-{Guid.NewGuid():N}";

        var (response, _) = await dual
            .POSTAsync<RoleCreateEndpoint, RoleCreateRequest, RoleCreateResponse>(new()
            {
                Name = name,
                Description = "Role created to prove which tenant the request acted in"
            });

        response.StatusCode.Should().Be(
            HttpStatusCode.OK,
            "the account administers the tenant it selected, so the request is one it may make");

        var stored = await DbContext.Roles
            .AcrossAllTenants()
            .AsNoTracking()
            .Where(role => role.NameNormalized == name.ToLowerInvariant())
            .Select(role => new { role.Id, role.TenantId })
            .ToListAsync(TestContext.Current.CancellationToken);

        stored.Should().ContainSingle("the request wrote into one tenant, not into both of them");
        stored[0].TenantId.Should().Be(selectedTenantId, "and it is the tenant the session selected");
        stored[0].TenantId.Should().NotBe(otherTenantId, "the other tenant the account belongs to was left untouched");
    }

    /// <summary>
    /// Verifies that a request made by an account that belongs to two tenants and has selected neither is
    /// refused with a named error code, and that the refusal is a refusal rather than an answer taken from
    /// whichever tenant the account happened to belong to first (AC-023).
    /// </summary>
    [Fact]
    public async Task No_Active_Tenant_Is_Refused()
    {
        // One role in each tenant the account belongs to, under a name nothing else can collide with, so
        // a response that leaked either tenant's rows would be recognisable as such rather than merely
        // non-empty.
        var inBootstrap = await CreateTenantRoleAsync(TestTenants.BootstrapTenantId, Allow.Role_View);
        var inSecondTenant = await CreateTenantRoleAsync(TestTenants.SecondTenantId, Allow.Role_View);
        var leakedNames = await DbContext.Roles
            .AcrossAllTenants()
            .AsNoTracking()
            .Where(role => role.Id == inBootstrap || role.Id == inSecondTenant)
            .Select(role => role.Name)
            .ToListAsync(TestContext.Current.CancellationToken);


        // No tenant named, so none is selected: the account holds more than one membership and sign-in
        // deliberately selects none of them.
        await SetAuthTokenAsync(DualMembershipUsername, TestUsers.DefaultPassword);

        var (response, problem) = await App.Client
            .GETAsync<RoleListEndpoint, RoleListRequest, ProblemDetails>(new() { All = true });

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        problem.Errors.Should().ContainSingle();
        problem.Errors.First().Code.Should().Be(
            ErrorCodes.NoActiveTenant,
            "the caller is told which of the four things went wrong so the web app can offer them the tenants they belong to");

        leakedNames.Should().NotBeEmpty("the arrangement has to have produced rows for their absence to mean anything");
        foreach (var leakedName in leakedNames)
        {
            body.Should().NotContain(leakedName, "the refusal is an explanation, not a page of rows from a tenant nobody selected");
        }
    }

    /// <summary>
    /// Verifies that an account holding no membership in any active tenant is refused by every
    /// tenant-scoped call with a code the client can translate into a message saying exactly that
    /// (AC-050).
    /// </summary>
    [Fact]
    public async Task Account_With_No_Membership_Is_Refused_With_An_Explanation()
    {
        await SetAuthTokenAsync(NoMembershipUsername, TestUsers.DefaultPassword);

        // Three surfaces rather than one, so what is proved is the standing of the caller rather than the
        // guard of a single endpoint.
        var (roleList, roleListProblem) = await App.Client
            .GETAsync<RoleListEndpoint, RoleListRequest, ProblemDetails>(new() { All = true });
        var (userList, userListProblem) = await App.Client
            .GETAsync<UserListEndpoint, UserListRequest, ProblemDetails>(new());
        var (notificationList, notificationListProblem) = await App.Client
            .GETAsync<NotificationListEndpoint, NotificationListRequest, ProblemDetails>(new());

        var refusals = new List<(HttpStatusCode Status, string? Code)>
        {
            (roleList.StatusCode, roleListProblem.Errors.FirstOrDefault()?.Code),
            (userList.StatusCode, userListProblem.Errors.FirstOrDefault()?.Code),
            (notificationList.StatusCode, notificationListProblem.Errors.FirstOrDefault()?.Code)
        };

        refusals.Should().OnlyContain(
            refusal => refusal.Status == HttpStatusCode.Forbidden,
            "an account that belongs to no active tenant is refused tenant-scoped work rather than served an empty page");

        refusals.Should().OnlyContain(
            refusal => refusal.Code == ErrorCodes.NoActiveTenant,
            "the body carries the code, so the client can say that the account belongs to no active tenant instead of showing a bare forbidden");
    }
}