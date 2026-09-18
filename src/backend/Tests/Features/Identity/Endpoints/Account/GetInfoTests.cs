namespace Backend.Tests.Features.Identity.Endpoints.Account;

using Backend.Features.Identity.Endpoints.Account;
using Backend.Tests.Features.Tenancy;

/// <summary>
/// Tests that the identity call reports the tenants the caller may work in and the one they are
/// working in right now - read from the session rather than from anything the browser keeps, so the
/// selection survives a reload and a second tab and is made once per session
/// (AC-024, AC-124).
/// </summary>
/// <remarks>
/// <para>
/// This is the one call the web application makes to learn who the caller is and where they may work:
/// signing in, reloading a page and switching tenant all read the same answer from the same place.
/// The two cases here are the two halves of that - the set to choose from, and the choice already
/// made - and both are asserted against the seeded <c>dual</c> account, the one account standing in
/// two tenants at once.
/// </para>
/// <para>
/// The selection is proved to live in the session and not in browser state by asking with the very
/// same access token from a second client that carries no cookie, no cache and no client state of its
/// own. A second client is a second tab: if the active tenant were resolved from anything the first
/// client held locally, the second would have to be told again, and it is not.
/// </para>
/// </remarks>
public class GetInfoTests(App app) : TenancyTestsBase(app)
{
    /// <summary>
    /// The seeded account holding a membership in both seeded tenants, so there is genuinely a choice
    /// to make and a set to report.
    /// </summary>
    private const string DualUsername = "dual";

    /// <summary>
    /// Verifies that signing in makes available the set of tenants the caller holds an active
    /// membership in, so the application can offer exactly those and nothing else (AC-024).
    /// </summary>
    [Fact]
    public async Task Returns_The_Callers_Tenants()
    {
        var client = await ClientForAsync(DualUsername);

        var (response, info) = await client.GETAsync<GetInfoEndpoint, UserGetInfoResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        info.Tenants.Select(tenant => tenant.Id).Should().BeEquivalentTo(
            [TestTenants.BootstrapTenantId, TestTenants.SecondTenantId],
            "these are the tenants this account holds an active membership in, and the set is the whole of them rather than a page of it");
        info.Tenants.Should().OnlyContain(tenant => tenant.Name.Length > 0 && tenant.Identifier.Length > 0,
            "each tenant is named well enough for the application chrome to show it and for a switch to address it");
        info.ActiveTenantId.Should().BeNull(
            "two tenants is a choice, and nothing makes it on the caller's behalf");
    }

    /// <summary>
    /// Verifies that the selected tenant is a property of the session rather than of the tab it was
    /// made in: the same access token presented by a second client reports the identical active
    /// tenant, so an ordinary reload or a new tab is not asked to choose again (AC-124).
    /// </summary>
    [Fact]
    public async Task Active_Tenant_Survives_A_Fresh_Client()
    {
        // The tenant is named here because this account belongs to two and sign-in therefore resolves
        // none by itself: the selection under test is the one this call establishes.
        var client = await ClientForAsync(DualUsername, TestTenants.SecondTenantId);

        var token = client.DefaultRequestHeaders.Authorization?.Parameter;
        token.Should().NotBeNullOrWhiteSpace("the selection is carried in the access token the session issued");

        var (firstResponse, first) = await client.GETAsync<GetInfoEndpoint, UserGetInfoResponse>();

        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        first.ActiveTenantId.Should().Be(TestTenants.SecondTenantId,
            "the tenant just selected is the one the caller is acting in");

        // A second client stands in for a second tab: it carries no cookie and no state of its own, so
        // the only thing it holds is the same access token.
        var secondClient = App.CreateClient(new ClientOptions { HandleCookies = false });
        TestsHelper.SetAuthToken(secondClient, token!);

        var (secondResponse, second) = await secondClient.GETAsync<GetInfoEndpoint, UserGetInfoResponse>();

        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        second.ActiveTenantId.Should().Be(first.ActiveTenantId,
            "the selection lives in the session, so the same session reports it wherever it is presented from");
        second.ActiveTenant!.Id.Should().Be(first.ActiveTenant!.Id,
            "and the tenant reported is the same tenant, named the same way, not merely a matching identifier");
        second.ActiveTenant!.Identifier.Should().Be(first.ActiveTenant!.Identifier);

        // The first client is unaffected by the second having asked, and neither of them is asked to
        // choose again: the selection stands for as long as the session does.
        var (againResponse, again) = await client.GETAsync<GetInfoEndpoint, UserGetInfoResponse>();

        againResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        again.ActiveTenantId.Should().Be(TestTenants.SecondTenantId);
    }

    /// <summary>
    /// Verifies that the roles reported while acting in a tenant include the caller's platform-scoped
    /// roles, not just that tenant's own. A platform-scoped role belongs to no tenant and the session
    /// check grants its permissions in every one, so leaving it out here would have the web
    /// application hide screens - and refuse navigation to them - that the API would have admitted.
    /// </summary>
    [Fact]
    public async Task Reports_Platform_Roles_While_Acting_In_A_Tenant()
    {
        var administrator = await SignInAsPlatformAdministratorActingInATenantAsync();

        var (response, info) = await App.Client.GETAsync<GetInfoEndpoint, UserGetInfoResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        info.Id.Should().Be(administrator.Id);
        info.ActiveTenantId.Should().NotBeNull("the account was signed in acting inside a tenant of its own");

        info.Roles.Select(role => role.Id).Should().Contain(
            TestRoles.PlatformAdminRoleId,
            "a platform-scoped role is held in every tenant, so acting in one does not hide it");

        info.Roles.SelectMany(role => role.Permissions).Select(permission => permission.Name).Should().Contain(
            Allow.Platform_Administration,
            "and what the web application computes from those roles is what the API will actually allow");
    }
}
