namespace Backend.Tests.FeatureManagement;

using Backend.Features.Identity.Core.Entities;
using Backend.Features.Identity.Endpoints.Account;
using Backend.Features.Identity.Endpoints.Permissions;
using Backend.Features.Identity.Endpoints.Users;

/// <summary>
/// The acceptance tests for the mint-time contract: a permission whose feature the tenant's plan
/// withholds is not minted into a session, is not offered on a role's surface, and is not reported to
/// the web app - while a session already issued keeps what it was issued with until it is renewed.
/// </summary>
public class SessionFeatureGatingTests(App app) : FeatureTestsBase(app)
{
    [Fact]
    public async Task A_Permission_Whose_Feature_Is_Off_Is_Never_Minted()
    {
        var tenant = await CreateTenantAsync();
        await SetForTenantAsync(tenant.Id, FeatureNames.Identity_UserManagement, "false");
        await SignInWithUserAdministrationAsync(tenant.Id);

        var permissions = await ReportedPermissionsAsync();

        permissions.Should().NotContain(Allow.User_Create);
        permissions.Should().Contain(Allow.User_View, "only the gated permission is withheld");
    }

    [Fact]
    public async Task With_The_Feature_On_The_Permission_Is_Minted_As_Before()
    {
        var tenant = await CreateTenantAsync();
        await SignInWithUserAdministrationAsync(tenant.Id);

        (await ReportedPermissionsAsync()).Should().Contain(Allow.User_Create);
    }

    [Fact]
    public async Task An_Edition_Withholding_A_Feature_Withholds_The_Permission_From_Its_Tenants()
    {
        var edition = await CreateEditionAsync();
        var tenant = await CreateTenantOnEditionAsync(edition.Id);
        await SetForEditionAsync(edition.Id, FeatureNames.Identity_UserManagement, "false");
        await SignInWithUserAdministrationAsync(tenant.Id);

        (await ReportedPermissionsAsync()).Should().NotContain(Allow.User_Create);
    }

    [Fact]
    public async Task The_Role_Permission_Surface_Does_Not_Offer_What_The_Plan_Withholds()
    {
        var tenant = await CreateTenantAsync();
        await SetForTenantAsync(tenant.Id, FeatureNames.FileManagement_Enabled, "false");
        await SignInWithUserAdministrationAsync(tenant.Id);

        var (response, catalogue) = await Client
            .GETAsync<GetDefinePermissionsEndpoint, GetDefinePermissionsResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        catalogue.Groups.Select(group => group.GroupName).Should().NotContain("File Management",
            "a permission the plan withholds cannot be granted, so offering it would be offering nothing");
    }

    [Fact]
    public async Task A_Session_Already_Issued_Keeps_What_It_Was_Issued_With()
    {
        var tenant = await CreateTenantAsync();
        var (_, roleId) = await SignInWithUserAdministrationAsync(tenant.Id);

        // Switched off after the token was minted. Authority is decided once, when a session is
        // issued, and every request until it is replaced is authorized from the claims it already
        // holds - so this call must still succeed. The window is bounded by Auth:AccessTokenValidity,
        // and this is exactly how a role change behaves today.
        await SetForTenantAsync(tenant.Id, FeatureNames.Identity_UserManagement, "false");

        var response = await CreateAUserAsync(roleId);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Renewing_The_Session_Is_What_Applies_The_Change()
    {
        var tenant = await CreateTenantAsync();
        var (account, roleId) = await SignInWithUserAdministrationAsync(tenant.Id);
        await SetForTenantAsync(tenant.Id, FeatureNames.Identity_UserManagement, "false");

        // Signing in again is the plainest renewal there is; a refresh and a tenant switch reach the
        // same code, because all three mint through SessionGrants.
        await SignInAsAsync(account.Username, tenant.Id);

        var response = await CreateAUserAsync(roleId);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ReportedPermissionsAsync()).Should().NotContain(Allow.User_Create);
    }

    [Fact]
    public async Task Switching_Into_A_Tenant_Applies_That_Tenants_Plan_And_Switching_Back_Restores_It()
    {
        var permissive = await CreateTenantAsync();
        var restrictive = await CreateTenantAsync();
        await SetForTenantAsync(restrictive.Id, FeatureNames.Identity_UserManagement, "false");

        var account = await CreateDualTenantMemberAsync(permissive.Id, restrictive.Id);
        var permissiveRole = await CreateTenantRoleAsync(permissive.Id, Allow.User_View, Allow.User_Create);
        var restrictiveRole = await CreateTenantRoleAsync(restrictive.Id, Allow.User_View, Allow.User_Create);
        await TenantAuthorizationService.ReplaceTenantRoleAssignmentsAsync(
            permissive.Id, account.Id, [permissiveRole], TestContext.Current.CancellationToken);
        await TenantAuthorizationService.ReplaceTenantRoleAssignmentsAsync(
            restrictive.Id, account.Id, [restrictiveRole], TestContext.Current.CancellationToken);

        await SignInAsAsync(account.Username, permissive.Id);
        (await ReportedPermissionsAsync()).Should().Contain(Allow.User_Create);

        await SwitchTenantAsync(restrictive.Id);
        (await ReportedPermissionsAsync()).Should().NotContain(Allow.User_Create,
            "the same identity holding the same permissions exercises what the tenant it acts in is entitled to");

        await SwitchTenantAsync(permissive.Id);
        (await ReportedPermissionsAsync()).Should().Contain(Allow.User_Create);
    }

    [Fact]
    public async Task A_Platform_Session_Is_Untouched_By_Any_Tenants_Plan()
    {
        var tenant = await CreateTenantAsync();
        await SetForTenantAsync(tenant.Id, FeatureNames.Identity_UserManagement, "false");
        await SetForTenantAsync(tenant.Id, FeatureNames.FileManagement_Enabled, "false");

        await SetPlatformAdminAuthTokenAsync();

        var permissions = await ReportedPermissionsAsync();

        permissions.Should().Contain(Allow.User_Create).And.Contain(Allow.File_Delete,
            "the platform tier is inside no plan, and the screen that switches a feature back on must never be one a feature hides");
    }

    [Fact]
    public async Task A_Platform_Account_Inside_A_Tenant_Is_Held_To_That_Tenants_Plan()
    {
        var tenant = await CreateTenantAsync();
        await SetForTenantAsync(tenant.Id, FeatureNames.Identity_UserManagement, "false");

        await SignInAsPlatformAdministratorEnteringAsync(tenant.Id);

        (await ReportedPermissionsAsync()).Should().NotContain(Allow.User_Create,
            "inside a tenant it exercises that tenant's tier, and the plan is part of what that tier is");
    }

    #region Helpers

    /// <summary>
    /// Creates an account in the tenant holding exactly the two account-administration permissions and
    /// nothing else, then signs the fixture's client in as it.
    /// </summary>
    private async Task<(User Account, Guid RoleId)> SignInWithUserAdministrationAsync(Guid tenantId)
    {
        var role = await CreateTenantRoleAsync(tenantId, Allow.User_View, Allow.User_Create);
        var account = await CreateTenantUserAsync(tenantId, role);
        await SignInAsAsync(account.Username, tenantId);
        return (account, role);
    }

    /// <summary>
    /// The permissions the account information endpoint reports. It is asked rather than the token
    /// inspected because the web app gates on exactly this answer, so a disagreement between it and
    /// the claims would be a bug these tests should catch.
    /// </summary>
    private async Task<IReadOnlyList<string>> ReportedPermissionsAsync()
    {
        var (response, info) = await Client.GETAsync<GetInfoEndpoint, UserGetInfoResponse>();
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return [.. info.Roles.SelectMany(role => role.Permissions).Select(permission => permission.Name).Distinct()];
    }

    /// <summary>
    /// Attempts to create an account. The role is named because the request requires one, so a payload
    /// without it would be refused for the wrong reason and prove nothing about entitlements.
    /// </summary>
    private async Task<HttpResponseMessage> CreateAUserAsync(Guid roleId)
    {
        var faker = new Faker<UserCreateRequest>()
            .RuleFor(u => u.Username, f => f.Internet.UserName() + Guid.NewGuid().ToString("N")[..8])
            .RuleFor(u => u.Password, f => f.Internet.Password())
            .RuleFor(u => u.FirstName, f => f.Name.FirstName())
            .RuleFor(u => u.LastName, f => f.Name.LastName())
            .RuleFor(u => u.IsActive, f => true);
        var request = faker.Generate();
        request.Email = $"{Guid.NewGuid():N}@example.com";
        request.Roles = [roleId];

        var (response, _) = await Client
            .POSTAsync<UserCreateEndpoint, UserCreateRequest, UserCreateResponse>(request);
        return response;
    }

    #endregion
}
