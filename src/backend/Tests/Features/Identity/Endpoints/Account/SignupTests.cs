namespace Backend.Tests.Features.Identity.Endpoints.Account;

using System.Text.Json;
using Backend.Data.Entities;
using Backend.Features.Identity.Core;
using Backend.Features.Identity.Core.Entities;
using Backend.Features.Identity.Endpoints.Account;
using Backend.Features.Identity.Endpoints.Users;
using Backend.Features.Tenancy.Endpoints.Tenants;
using Backend.Tests.Features.Tenancy;

/// <summary>
/// Tests what self-service sign-up produces: a global account that belongs to no tenant, that reaches
/// the self-service surfaces without one, that is granted no role and no platform permission, and
/// that is treated afterwards as an authenticated user with no usable membership
/// (AC-118, AC-119, AC-120, AC-122).
/// </summary>
/// <remarks>
/// <para>
/// Sign-up is the one door into the application that is opened from outside every tenant, so what it
/// hands out is a bare authenticated identity: an account, and nothing attached to it. Creating a
/// tenant is a separate act the new account performs afterwards, which is what makes the authority it
/// ends up with something it obtained deliberately rather than something sign-up conferred.
/// </para>
/// <para>
/// The invented accounts here are named from a fresh identifier rather than a counter, because
/// usernames and emails are globally unique and the test database is neither wiped nor recreated
/// between runs: a counter that restarts with the process would collide with the previous run's rows.
/// </para>
/// </remarks>
public class SignupTests(App app) : TenancyTestsBase(app)
{
    /// <summary>
    /// The password every account created by these tests signs up with, so a case that needs to sign
    /// in as the account it just created knows the credentials it used.
    /// </summary>
    private const string SignupPassword = "Signup#123";

    /// <summary>
    /// Verifies that self-service sign-up creates a global account holding no membership and joining
    /// no existing tenant (AC-118).
    /// </summary>
    [Fact]
    public async Task Creates_A_Global_Account_With_No_Membership()
    {
        var (account, username, _) = await SignUpAsync();

        account.Username.Should().Be(username);
        account.Email.Should().Be($"{username}@example.com");
        account.IsActive.Should().BeTrue("the account is created in service, which is what lets it sign in at once");

        var memberships = await DbContext.TenantMemberships
            .AcrossAllTenants()
            .AsNoTracking()
            .Where(membership => membership.UserId == account.Id)
            .Select(membership => membership.TenantId)
            .ToListAsync(TestContext.Current.CancellationToken);

        memberships.Should().BeEmpty(
            "signing up joins no tenant - not the bootstrap tenant, not any other - so the account starts outside every one of them");
    }

    /// <summary>
    /// Verifies that self-service sign-up completes normally when no tenant context is established,
    /// which is the only state an anonymous visitor can arrive in (AC-119).
    /// </summary>
    [Fact]
    public async Task Works_With_No_Tenant_Context()
    {
        var username = $"signup-{Guid.NewGuid():N}";

        // A client of its own, presenting no bearer token and no cookie: nothing about this request
        // establishes a tenant, since establishing one is the very thing a visitor cannot yet do.
        var visitor = App.CreateClient(new ClientOptions { HandleCookies = false });

        var (response, _) = await visitor
            .POSTAsync<SignupEndpoint, SignupRequest, SignupResponse>(new()
            {
                Username = username,
                Email = $"{username}@example.com",
                Password = SignupPassword,
                ConfirmPassword = SignupPassword
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "sign-up is account self-service, so the one answer it must never give is the noActiveTenant refusal a tenant-scoped call makes");

        var created = await DbContext.Users
            .AsNoTracking()
            .AnyAsync(user => user.UsernameNormalized == username, TestContext.Current.CancellationToken);

        created.Should().BeTrue("the account was created rather than the request being turned away");
    }

    /// <summary>
    /// Verifies that an account created by self-service sign-up is granted no tenant-scoped role and
    /// no platform permission, at sign-up and through the self-service onboarding that can follow it
    /// (AC-120).
    /// </summary>
    [Fact]
    public async Task Grants_No_Role_And_No_Platform_Permission()
    {
        var (account, username, password) = await SignUpAsync();

        var heldAssignments = await DbContext.UserRoles
            .AsNoTracking()
            .CountAsync(assignment => assignment.UserId == account.Id, TestContext.Current.CancellationToken);

        heldAssignments.Should().Be(0, "sign-up grants an authenticated identity and nothing else");

        var client = await SignedInClientAsync(username, password);

        var (beforeResponse, before) = await client.GETAsync<GetInfoEndpoint, UserGetInfoResponse>();

        beforeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        before.Tenants.Should().BeEmpty("the account belongs to no tenant, so there is nothing to act in");
        before.Roles.Should().BeEmpty("and no role is held at any scope");
        before.IsPlatformAdministrator.Should().BeFalse(
            "platform administration is never handed out by signing up, which is anonymous and self-service");

        PermissionClaimsOf(AccessTokenOf(client)).Should().BeEmpty(
            "the session sign-up leads to carries no permission claim at all");

        // The second half of the criterion: the self-service onboarding that an account with no tenant
        // may perform gives it authority inside the tenant it creates and never at platform level.
        var identifier = NewTenantIdentifier();
        var (onboardResponse, onboarded) = await client
            .POSTAsync<TenantOnboardEndpoint, TenantOnboardRequest, TenantOnboardResponse>(new()
            {
                Name = $"Tenant {Guid.NewGuid():N}",
                Identifier = identifier
            });

        onboardResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        onboarded.Identifier.Should().Be(identifier);

        var platformPermissions = App.Services
            .GetRequiredService<IPermissionDefinitionService>()
            .GetPlatformPermissionNames();

        platformPermissions.Should().NotBeEmpty("the catalogue declares which permissions are platform level, and the comparison below is only meaningful if it declares some");

        PermissionClaimsOf(onboarded.Session.AccessToken).Should().NotIntersectWith(platformPermissions,
            "onboarding creates a tenant and makes the caller its first administrator; the authority that comes with it is that tenant's and reaches nothing platform-wide");

        TestsHelper.SetAuthToken(client, onboarded.Session.AccessToken);

        var (afterResponse, after) = await client.GETAsync<GetInfoEndpoint, UserGetInfoResponse>();

        afterResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        after.ActiveTenantId.Should().Be(onboarded.Id, "the tenant just created is the one the caller now acts in");
        after.IsPlatformAdministrator.Should().BeFalse(
            "the caller administers a tenant, which is not the same standing as administering the platform");
        after.Roles.SelectMany(role => role.Permissions).Select(permission => permission.Name)
            .Should().NotIntersectWith(platformPermissions,
                "every permission held comes through the new tenant's own role, and a tenant-tier role carries no platform permission");
    }

    /// <summary>
    /// Verifies that an account created by self-service sign-up is, until it joins a tenant, an
    /// authenticated user with no usable membership: account self-service and self-service tenant
    /// creation answer it while every other tenant-scoped operation is refused (AC-122).
    /// </summary>
    [Fact]
    public async Task New_Account_Is_An_Authenticated_User_With_No_Usable_Membership()
    {
        var (account, username, password) = await SignUpAsync();
        var client = await SignedInClientAsync(username, password);

        // Authenticated, and therefore told which tenant it is missing rather than asked who it is.
        var (refused, refusal) = await client
            .GETAsync<UserListEndpoint, UserListRequest, ProblemDetails>(new());

        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "the account belongs to no active tenant, and reading accounts is tenant-scoped work");
        refused.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized, "the account is authenticated; it is the tenant that is missing");
        refusal.Errors.Should().ContainSingle();
        refusal.Errors.First().Code.Should().Be(ErrorCodes.NoActiveTenant);

        // Account self-service answers it: reading one's own profile is about the person, not a tenant.
        var (profileResponse, profile) = await client.GETAsync<ProfileEndpoint, UserProfileResponse>();

        profileResponse.StatusCode.Should().Be(HttpStatusCode.OK, "the caller is still who they are");
        profile.Id.Should().Be(account.Id);

        // And self-service tenant creation is the door out of the state, exactly as it is for any other
        // account holding no usable membership.
        var identifier = NewTenantIdentifier();
        var (onboardResponse, onboarded) = await client
            .POSTAsync<TenantOnboardEndpoint, TenantOnboardRequest, TenantOnboardResponse>(new()
            {
                Name = $"Tenant {Guid.NewGuid():N}",
                Identifier = identifier
            });

        onboardResponse.StatusCode.Should().Be(HttpStatusCode.OK, "creating a tenant is available to a caller with no tenant");
        onboarded.Identifier.Should().Be(identifier);

        // The membership created by onboarding is usable at once: the tenant-scoped call refused above
        // is answered for the same caller, in the tenant it just created.
        TestsHelper.SetAuthToken(client, onboarded.Session.AccessToken);

        var (admitted, _) = await client
            .GETAsync<UserListEndpoint, UserListRequest, UserListResponse>(new());

        admitted.StatusCode.Should().Be(HttpStatusCode.OK,
            "the account is now a member of a tenant and administers it, which is what the refusal above said it lacked");
    }

    /// <summary>
    /// Signs a visitor up through the endpoint, so the account under test is the one the production
    /// path creates rather than one assembled by the test.
    /// </summary>
    /// <returns>The stored account, and the credentials it signed up with.</returns>
    private async Task<(User Account, string Username, string Password)> SignUpAsync()
    {
        var username = $"signup-{Guid.NewGuid():N}";
        var visitor = App.CreateClient(new ClientOptions { HandleCookies = false });

        var (response, _) = await visitor
            .POSTAsync<SignupEndpoint, SignupRequest, SignupResponse>(new()
            {
                Username = username,
                Email = $"{username}@example.com",
                Password = SignupPassword,
                ConfirmPassword = SignupPassword
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK, "sign-up is answered without a tenant");

        var account = await DbContext.Users
            .AsNoTracking()
            .SingleAsync(user => user.UsernameNormalized == username, TestContext.Current.CancellationToken);

        return (account, username, SignupPassword);
    }

    /// <summary>
    /// A client presenting the signed-up account's own credentials, for the cases that go on to ask
    /// what that account may do.
    /// </summary>
    /// <param name="username">The account to sign in as.</param>
    /// <param name="password">The password it signed up with.</param>
    /// <returns>A client presenting that account's bearer token and nothing else.</returns>
    private async Task<HttpClient> SignedInClientAsync(string username, string password)
    {
        var client = App.CreateClient(new ClientOptions { HandleCookies = false });
        await TestsHelper.SetNewAuthTokenAsync(client, username, password);
        return client;
    }

    /// <summary>
    /// The bearer token a client is presenting.
    /// </summary>
    /// <param name="client">The client to read.</param>
    /// <returns>The access token.</returns>
    private static string AccessTokenOf(HttpClient client)
    {
        var token = client.DefaultRequestHeaders.Authorization?.Parameter;
        token.Should().NotBeNullOrWhiteSpace("the client was signed in, so it has a token to read");
        return token!;
    }

    /// <summary>
    /// The permission claims an access token carries, read out of the token itself rather than out of
    /// anything derived from it - so what is asserted is what the session was issued with.
    /// </summary>
    /// <param name="accessToken">The token to read.</param>
    /// <returns>The permission names it carries, empty when it carries no permission claim at all.</returns>
    private static List<string> PermissionClaimsOf(string accessToken)
    {
        var payload = accessToken.Split('.')[1].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight(payload.Length + ((4 - (payload.Length % 4)) % 4), '=');

        using var document = JsonDocument.Parse(Convert.FromBase64String(payload));

        if (!document.RootElement.TryGetProperty(ClaimConstants.Permission, out var claims))
        {
            return [];
        }

        return claims.ValueKind == JsonValueKind.Array
            ? [.. claims.EnumerateArray().Select(claim => claim.GetString()!).Where(name => name.Length > 0)]
            : [claims.GetString()!];
    }
}
