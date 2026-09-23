namespace Backend.Tests.Features.Identity.Core;

using Backend.Features.Tenancy.Core;
using Backend.Features.Identity.Endpoints.Account;
using Backend.Features.Tenancy.Endpoints.Tenants;
using Backend.Tests.Features.Tenancy;

/// <summary>
/// Tests the one rule every authorization decision now rests on: a session carries only the
/// permissions of the scope it is acting in. A platform account acting in no tenant exercises the
/// platform scope and the permissions declared for both; anyone acting inside a tenant exercises the
/// tenant scope and those same both-scope permissions; and an ordinary account with no active tenant
/// exercises nothing at all.
/// </summary>
/// <remarks>
/// The permissions are read off the account-info call rather than off the endpoints they gate, because
/// that is what the web application computes its own gating from: if the two disagreed, the client
/// would offer screens the API refuses, or hide ones it would have allowed. The endpoints are covered
/// where they live - what is stated here is the rule underneath all of them.
/// </remarks>
public class PermissionScopeTests(App app) : TenancyTestsBase(app)
{
    /// <summary>
    /// Verifies the whole round trip a platform account makes: it holds its platform roles' authority
    /// while acting in no tenant, exchanges it for the roles its membership holds on entering a tenant
    /// it belongs to, and holds it again the moment it leaves - without signing in again at any point
    /// (AC-108, AC-113).
    /// </summary>
    /// <remarks>
    /// Both directions are asserted. A narrowing that ran only one way would leave a platform account
    /// inside a tenant still holding the authority to delete it; one that never restored would leave it
    /// unable to work on the platform after a single visit to a tenant. Inside, the account holds only
    /// what its tenant role grants - a both-scope permission its platform role also holds is absent
    /// there, because platform roles count only in platform scope. The tier itself is asserted
    /// throughout, because it is what makes the way back out available at all.
    /// </remarks>
    [Fact]
    public async Task Platform_Account_Exchanges_Its_Scope_For_A_Tenants_And_Back()
    {
        var tenant = await CreateTenantAsync();
        var tenantRole = await CreateTenantRoleAsync(tenant.Id, Allow.TenantMember_View);

        var account = await CreateTenantUserAsync(tenant.Id, tenantRole);
        await UserService.AssignRoleAsync(account.Id, TestRoles.PlatformAdminRoleId);
        await MarkAsPlatformAccountAsync(account.Id);

        // Named no tenant, a platform account signs in to platform scope whatever it belongs to.
        await SignInAsAsync(account.Username);

        var outside = await PermissionsAsync();

        outside.Should().Contain(Allow.Tenant_Create,
            "acting in no tenant is where a platform-scoped permission is exercised");
        outside.Should().Contain(Allow.User_View,
            "and a permission declared for both scopes is exercised there too, over the platform's own accounts");

        await SwitchTenantAsync(tenant.Id);

        var inside = await PermissionsAsync();

        inside.Should().NotContain(Allow.Tenant_Create,
            "entering a tenant makes the caller that tenant's actor, and creating tenants is not a tenant's to do");
        inside.Should().NotContain(Allow.User_View,
            "the platform role holds it, but inside a tenant only the tenant's own roles count");
        inside.Should().Contain(Allow.TenantMember_View,
            "what the caller holds in there is what its tenant role grants");

        var (left, exit) = await Client.POSTAsync<TenantExitEndpoint, TenantExitResponse>();
        left.StatusCode.Should().Be(HttpStatusCode.OK,
            "the account tier survives entering a tenant, which is what keeps the way back out open");

        // Leaving re-establishes the session rather than editing the one in hand, so the client presents
        // what it was handed back. A browser does this by itself - the re-signed cookie carries it - and
        // a token client replaces its pair, which is what this stands in for.
        TestsHelper.SetAuthToken(Client, exit.Session.AccessToken);

        var back = await PermissionsAsync();

        back.Should().Contain(Allow.Tenant_Create,
            "leaving restores the platform scope, so the authority narrowed away on the way in is held again");
        back.Should().Contain(Allow.User_View, "and the both-scope permissions were never lost");
    }

    /// <summary>
    /// Verifies that an ordinary account acting in no tenant exercises nothing, so the platform scope
    /// is the platform's and not merely what is left when no tenant is selected (AC-050).
    /// </summary>
    /// <remarks>
    /// The account is given a platform-scoped role granting a permission - the strongest form of the
    /// case. An account holding no role at all would have an empty permission set for a reason that has
    /// nothing to do with the scope rule, and would pass whether the rule existed or not.
    /// <para>
    /// The state is reached the way it actually arises: an ordinary account cannot sign in without a
    /// tenant, so it signs in to its own and loses it - here by suspension - at the next renewal. What
    /// is left is an ordinary account acting in no tenant, which is the standing under test.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Ordinary_Account_With_No_Active_Tenant_Exercises_Nothing()
    {
        var tenant = await CreateTenantAsync();
        var account = await CreateTenantUserAsync(tenant.Id);

        await UserService.AssignRoleAsync(account.Id, TestRoles.PlatformAdminRoleId);

        var session = await SessionForAsync(account.Username, tenant.Id);

        await TenantScopedAsync(tenant.Id, async () =>
        {
            var row = await DbContext.Tenants.SingleAsync(candidate => candidate.Id == tenant.Id, TestContext.Current.CancellationToken);
            row.Status = TenantStatus.Suspended;
            await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        await session.RenewAsync();

        var (response, info) = await session.Client.GETAsync<GetInfoEndpoint, UserGetInfoResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        info.ActiveTenantId.Should().BeNull("the renewal dropped the suspended tenant, so there is none to act in");
        info.IsPlatform.Should().BeFalse("and the account was never made one of the platform's own");

        info.Roles.Should().NotBeEmpty(
            "the premise is that the account does hold a role: an account holding none would have nothing to narrow away");
        info.Roles.SelectMany(role => role.Permissions).Should().BeEmpty(
            "acting in no tenant without belonging to the platform is not a scope to work in, so the role grants nothing here");
    }

    /// <summary>
    /// The permission names the caller's session currently exercises, read from the account-info call.
    /// </summary>
    /// <returns>The permission names, without duplicates.</returns>
    private async Task<List<string>> PermissionsAsync()
    {
        var info = await InfoAsync();

        return [.. info.Roles.SelectMany(role => role.Permissions).Select(permission => permission.Name).Distinct(StringComparer.Ordinal)];
    }

    /// <summary>
    /// The account-info answer for the caller the fixture's client is signed in as.
    /// </summary>
    /// <returns>The answer.</returns>
    private async Task<UserGetInfoResponse> InfoAsync()
    {
        var (response, info) = await Client.GETAsync<GetInfoEndpoint, UserGetInfoResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return info;
    }
}
