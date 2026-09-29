namespace Backend.Tests.Features.Identity.Sessions;

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Backend.Features.Identity.Core;
using Backend.Features.Identity.Core.Sessions;
using Backend.Features.Identity.Endpoints.Account;
using Backend.Features.Tenancy.Endpoints.Tenants;
using Backend.Tests.Features.Tenancy;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;

/// <summary>
/// Tests that a token and an auth cookie say who the caller is and which session it is, and nothing
/// else - every grant lives in the session store - and that the session store is what a signed-in
/// caller's standing is read from.
/// </summary>
public class SessionTokenTests(App app) : TenancyTestsBase(app)
{
    /// <summary>
    /// The registered JWT claims a token legitimately carries beside the two that matter.
    /// </summary>
    private static readonly string[] RegisteredClaims = ["iss", "aud", "exp", "nbf", "iat"];

    /// <summary>
    /// Verifies the access token's payload holds the account and the session and no grant: the keys are
    /// the name-identifier claim type and the literal <c>sid</c>, and nothing about roles, permissions, tenant, tier, name
    /// or email is in it.
    /// </summary>
    [Fact]
    public async Task Access_Token_Holds_Only_The_Account_And_The_Session()
    {
        var (_, tokens) = await SignInAsync(TestUsers.TenantAdminUsername, TestUsers.AdminPassword);

        var payload = TestsHelper.PayloadOf(tokens.AccessToken);

        payload.Keys.Order().Should().Equal(string.Join(",", RegisteredClaims).Split(',').Append(System.Security.Claims.ClaimTypes.NameIdentifier).Append("sid").Order(),
            "the token names the caller and the session; what the session grants is read from the store on every request");
        payload[ClaimTypes.NameIdentifier].GetString().Should().Be(TestUsers.TenantAdminUserId.ToString());
        payload["sid"].GetString().Should().MatchRegex("^[0-9a-f]{32}$", "the session identifier is an opaque random value");
    }

    /// <summary>
    /// Verifies the auth cookie's ticket holds only the account and the session, exactly as the token does.
    /// </summary>
    [Fact]
    public async Task Cookie_Ticket_Holds_Only_The_Account_And_The_Session()
    {
        var (response, tokens) = await SignInAsync(TestUsers.TenantAdminUsername, TestUsers.AdminPassword);

        var ticket = UnprotectAuthCookie(response);

        ticket.Principal.Claims.Select(claim => claim.Type).Should().BeEquivalentTo([ClaimTypes.NameIdentifier, "sid"]);
        ticket.Principal.FindFirst(ClaimTypes.NameIdentifier)!.Value.Should().Be(TestUsers.TenantAdminUserId.ToString());
        ticket.Principal.FindFirst("sid")!.Value.Should().Be(TestsHelper.PayloadOf(tokens.AccessToken)["sid"].GetString(),
            "the cookie and the token of one sign-in name the same session");
    }

    /// <summary>
    /// Verifies the stored session holds what the token used to: the tenant, the role and permission
    /// names, the account's names and its tier.
    /// </summary>
    [Fact]
    public async Task Stored_Session_Holds_The_Grants()
    {
        var (_, tokens) = await SignInAsync(TestUsers.TenantAdminUsername, TestUsers.AdminPassword);

        var session = await SessionOfAsync(tokens.AccessToken);

        session.Should().NotBeNull();
        session!.UserId.Should().Be(TestUsers.TenantAdminUserId);
        session.TenantId.Should().Be(TestTenants.BootstrapTenantId);
        session.Username.Should().Be(TestUsers.TenantAdminUsername);
        session.Email.Should().NotBeNullOrWhiteSpace();
        session.IsPlatform.Should().BeFalse();
        session.Roles.Should().NotBeEmpty();
        session.Permissions.Should().NotBeEmpty();
        session.ExpiresAt.Should().BeAfter(session.CreatedAt);
    }

    /// <summary>
    /// Verifies a request is authorized on what the stored session holds, not on the token: the
    /// projected claims are what <c>Permissions(...)</c> and the current-user service read.
    /// </summary>
    [Fact]
    public async Task Get_Info_Reports_The_Sessions_Own_Roles_And_Permissions()
    {
        await SetAuthTokenAsync();
        var token = Client.DefaultRequestHeaders.Authorization!.Parameter!;

        var (response, info) = await Client.GETAsync<GetInfoEndpoint, UserGetInfoResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var session = (await SessionOfAsync(token))!;
        info.Roles.Select(role => role.Name).Should().BeEquivalentTo(session.Roles);
        info.Roles.SelectMany(role => role.Permissions).Select(permission => permission.Name)
            .Should().BeEquivalentTo(session.Permissions);
    }

    /// <summary>
    /// Verifies <c>get-info</c> reports exactly the session's roles and permissions when the roles change
    /// after the session was minted: a permission removed from a role, a role renamed, and a role deleted.
    /// The database only adds identifiers and display names; it never drops what the session holds.
    /// </summary>
    [Fact]
    public async Task Get_Info_Reports_Exactly_The_Session_Whatever_Happened_To_The_Roles()
    {
        var tenant = await CreateTenantAsync();
        var keptRoleId = await CreateTenantRoleAsync(tenant.Id, Allow.User_View, Allow.Role_View);
        var renamedRoleId = await CreateTenantRoleAsync(tenant.Id, Allow.Tenant_Detail);
        var deletedRoleId = await CreateTenantRoleAsync(tenant.Id, Allow.User_Create);
        var account = await CreateTenantUserAsync(tenant.Id, keptRoleId, renamedRoleId, deletedRoleId);
        await SignInAsAsync(account.Username, tenant.Id);
        var session = (await SessionOfAsync(Client.DefaultRequestHeaders.Authorization!.Parameter!))!;
        session.Roles.Should().HaveCountGreaterThanOrEqualTo(3);

        var cancellationToken = TestContext.Current.CancellationToken;
        await DbContext.RolePermissions
            .Where(rolePermission => rolePermission.RoleId == keptRoleId && rolePermission.Permission.Name == Allow.User_View)
            .ExecuteDeleteAsync(cancellationToken);
        await DbContext.Roles.AcrossAllTenants().Where(role => role.Id == renamedRoleId)
            .ExecuteUpdateAsync(set => set.SetProperty(role => role.Name, role => role.Name + " renamed"), cancellationToken);
        await DbContext.RolePermissions.Where(rolePermission => rolePermission.RoleId == deletedRoleId).ExecuteDeleteAsync(cancellationToken);
        await DbContext.UserRoles.Where(assignment => assignment.RoleId == deletedRoleId).ExecuteDeleteAsync(cancellationToken);
        await DbContext.Roles.AcrossAllTenants().Where(role => role.Id == deletedRoleId).ExecuteDeleteAsync(cancellationToken);

        var (response, info) = await Client.GETAsync<GetInfoEndpoint, UserGetInfoResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        info.Roles.Select(role => role.Name).Should().BeEquivalentTo(session.Roles);
        info.Roles.SelectMany(role => role.Permissions).Select(permission => permission.Name)
            .Should().BeEquivalentTo(session.Permissions);
        info.Roles.SelectMany(role => role.Permissions).Should().OnlyContain(permission => permission.DisplayName.Length > 0);
    }

    /// <summary>
    /// Verifies a token whose session is not in the store is refused with 401, on the bearer path.
    /// </summary>
    [Fact]
    public async Task Bearer_Token_Whose_Session_Is_Gone_Answers_401()
    {
        await SetAuthTokenAsync();
        var token = Client.DefaultRequestHeaders.Authorization!.Parameter!;

        (await Client.GETAsync<GetInfoEndpoint, ProblemDetails>()).Response.StatusCode.Should().Be(HttpStatusCode.OK);

        await SessionStore.DeleteAsync(SessionIdOf(token), TestContext.Current.CancellationToken);

        var (response, problem) = await Client.GETAsync<GetInfoEndpoint, ProblemDetails>();

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        problem.Errors.Single().Code.Should().Be(ErrorCodes.AuthenticationRequired.Value);
    }

    /// <summary>
    /// Verifies the same on the cookie path: the cookie alone authenticates until its session is deleted.
    /// </summary>
    [Fact]
    public async Task Cookie_Whose_Session_Is_Gone_Answers_401()
    {
        var (response, tokens) = await SignInAsync(TestUsers.TenantAdminUsername, TestUsers.AdminPassword);
        var client = App.CreateClient(new ClientOptions { HandleCookies = false });
        client.DefaultRequestHeaders.Add("Cookie", AuthCookieHeader(response));

        (await client.GETAsync<GetInfoEndpoint, ProblemDetails>()).Response.StatusCode.Should().Be(HttpStatusCode.OK,
            "the cookie authenticates on its own, and what the request may do is read from the stored session");

        await SessionStore.DeleteAsync(SessionIdOf(tokens.AccessToken), TestContext.Current.CancellationToken);

        (await client.GETAsync<GetInfoEndpoint, ProblemDetails>()).Response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Verifies a session that belongs to another account than the token names is not honoured.
    /// </summary>
    [Fact]
    public async Task Session_Of_Another_Account_Answers_401()
    {
        await SetAuthTokenAsync();
        var sessionId = SessionIdOf(Client.DefaultRequestHeaders.Authorization!.Parameter!);
        var session = (await SessionStore.GetAsync(sessionId, TestContext.Current.CancellationToken))!;

        await SessionStore.DeleteAsync(sessionId, TestContext.Current.CancellationToken);
        await SessionStore.CreateAsync(new SessionRecord
        {
            SessionId = sessionId,
            UserId = TestUsers.TestUserId,
            Username = session.Username,
            Email = session.Email,
            TenantId = session.TenantId,
            Roles = session.Roles,
            Permissions = session.Permissions,
            CreatedAt = session.CreatedAt,
            ExpiresAt = session.ExpiresAt,
        }, TestContext.Current.CancellationToken);

        (await Client.GETAsync<GetInfoEndpoint, ProblemDetails>()).Response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Verifies a token of the old shape - carrying its grants and no session - is refused: a token that
    /// names no session cannot be validated, so it authenticates nobody.
    /// </summary>
    [Fact]
    public async Task Token_Without_A_Session_Answers_401()
    {
        var setting = Service<IOptions<AuthSetting>>().Value;
        var token = JwtBearer.CreateToken(options =>
        {
            options.SigningKey = setting.Jwt.Key;
            options.Issuer = setting.Jwt.Issuer;
            options.Audience = setting.Jwt.Audience;
            options.ExpireAt = DateTime.UtcNow.AddMinutes(5);
            options.User.Claims.Add((ClaimTypes.NameIdentifier, TestUsers.TenantAdminUserId.ToString()));
            options.User.Claims.Add((ClaimConstants.Permission, Allow.User_View));
            options.User.Claims.Add((ClaimConstants.TenantId, TestTenants.BootstrapTenantId.ToString()));
        });
        TestsHelper.SetAuthToken(Client, token);

        (await Client.GETAsync<GetInfoEndpoint, ProblemDetails>()).Response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Verifies signing out deletes the session, so the access token that signed out stops working at once
    /// instead of when it would have expired.
    /// </summary>
    [Fact]
    public async Task Signed_Out_Access_Token_Answers_401_Immediately()
    {
        var tenant = await CreateTenantAsync();
        var account = await CreateTenantUserAsync(tenant.Id);
        await SignInAsAsync(account.Username, tenant.Id);
        var token = Client.DefaultRequestHeaders.Authorization!.Parameter!;
        var sessionId = SessionIdOf(token);

        var (signout, _) = await Client.POSTAsync<SignoutEndpoint, EmptyResponse>();

        signout.StatusCode.Should().Be(HttpStatusCode.OK);
        (await SessionStore.GetAsync(sessionId, TestContext.Current.CancellationToken)).Should().BeNull();
        (await Client.GETAsync<GetInfoEndpoint, ProblemDetails>()).Response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Verifies a refresh deletes the session it replaces and creates a new one, and that the access
    /// token naming the replaced session stops working while the renewed one works.
    /// </summary>
    [Fact]
    public async Task Refresh_Replaces_The_Session()
    {
        var account = await CreateTenantUserAsync((await CreateTenantAsync()).Id);
        var session = await SessionForAsync(account.Username);
        var oldToken = session.Client.DefaultRequestHeaders.Authorization!.Parameter!;
        var oldSessionId = SessionIdOf(oldToken);

        var newToken = await session.RenewAsync();
        var newSessionId = SessionIdOf(newToken);

        newSessionId.Should().NotBe(oldSessionId);
        (await SessionStore.GetAsync(oldSessionId, TestContext.Current.CancellationToken)).Should().BeNull("the renewal deleted the session it replaced");
        (await SessionStore.GetAsync(newSessionId, TestContext.Current.CancellationToken)).Should().NotBeNull();
        (await DbContext.AuthTokens.AsNoTracking().AcrossAllTenants()
                .Where(row => row.UserId == account.Id).Select(row => row.SessionId)
                .ToListAsync(TestContext.Current.CancellationToken))
            .Should().Equal([newSessionId], "the refresh-token row names the session it belongs to, and the consumed one is gone");

        var stale = App.CreateClient(new ClientOptions { HandleCookies = false });
        TestsHelper.SetAuthToken(stale, oldToken);
        (await stale.GETAsync<GetInfoEndpoint, ProblemDetails>()).Response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await session.Client.GETAsync<GetInfoEndpoint, ProblemDetails>()).Response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Verifies switching tenant deletes the session it leaves and mints one for the tenant entered.
    /// </summary>
    [Fact]
    public async Task Switch_Replaces_The_Session()
    {
        var first = await CreateTenantAsync();
        var second = await CreateTenantAsync();
        var account = await CreateTenantUserAsync(first.Id, await CreateTenantRoleAsync(first.Id, Allow.User_View));
        await MembershipService.AddAsync(second.Id, account.Id, [await CreateTenantRoleAsync(second.Id, Allow.User_View)], TestContext.Current.CancellationToken);

        await SignInAsAsync(account.Username, first.Id);
        var oldToken = Client.DefaultRequestHeaders.Authorization!.Parameter!;
        var stale = App.CreateClient(new ClientOptions { HandleCookies = false });
        TestsHelper.SetAuthToken(stale, oldToken);

        var newToken = await TestsHelper.SwitchTenantAsync(Client, second.Id);

        (await SessionStore.GetAsync(SessionIdOf(oldToken), TestContext.Current.CancellationToken)).Should().BeNull();
        (await SessionOfAsync(newToken))!.TenantId.Should().Be(second.Id);
        (await stale.GETAsync<GetInfoEndpoint, ProblemDetails>()).Response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Client.GETAsync<GetInfoEndpoint, ProblemDetails>()).Response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Verifies exiting a tenant for platform scope deletes the session it leaves and mints one that
    /// acts in no tenant.
    /// </summary>
    [Fact]
    public async Task Exit_Replaces_The_Session()
    {
        var tenant = await CreateTenantAsync();
        await SignInAsPlatformAdministratorEnteringAsync(tenant.Id);
        var oldToken = Client.DefaultRequestHeaders.Authorization!.Parameter!;

        var (response, exited) = await Client.POSTAsync<TenantExitEndpoint, TenantExitResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await SessionStore.GetAsync(SessionIdOf(oldToken), TestContext.Current.CancellationToken)).Should().BeNull();
        var session = (await SessionOfAsync(exited.Session.AccessToken))!;
        session.TenantId.Should().BeNull();
        session.IsPlatform.Should().BeTrue();
    }

    /// <summary>
    /// Verifies the store being the wired one is the shared in-memory store under the Testing
    /// environment, so the suite needs nothing but PostgreSQL.
    /// </summary>
    [Fact]
    public void Testing_Host_Runs_On_The_In_Memory_Store()
    {
        Service<ISessionStore>().Should().BeOfType<Fakes.FaultInjectingSessionStore>()
            .Which.Inner.Should().BeOfType<InMemorySessionStore>();
    }

    private async Task<(HttpResponseMessage Response, TokenResponse Tokens)> SignInAsync(string username, string password)
    {
        var client = App.CreateClient(new ClientOptions { HandleCookies = false });
        var (response, tokens) = await client.POSTAsync<TokenEndpoint, TokenRequest, TokenResponse>(new() { Username = username, Password = password });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (response, tokens);
    }

    private static string SessionIdOf(string accessToken) => TestsHelper.PayloadOf(accessToken)["sid"].GetString()!;

    private string AuthCookieName => Service<IOptionsMonitor<CookieAuthenticationOptions>>()
        .Get(CookieAuthenticationDefaults.AuthenticationScheme).Cookie.Name!;

    /// <summary>
    /// The <c>name=value</c> pair of the auth cookie a sign-in set, as a request would send it back.
    /// </summary>
    private string AuthCookieHeader(HttpResponseMessage response)
    {
        var cookie = response.Headers.GetValues("Set-Cookie")
            .Select(header => header.Split(';')[0])
            .Single(pair => pair.StartsWith(AuthCookieName + "=", StringComparison.Ordinal));
        return cookie;
    }

    private AuthenticationTicket UnprotectAuthCookie(HttpResponseMessage response)
    {
        var options = Service<IOptionsMonitor<CookieAuthenticationOptions>>().Get(CookieAuthenticationDefaults.AuthenticationScheme);
        var value = AuthCookieHeader(response)[(AuthCookieName.Length + 1)..];
        return options.TicketDataFormat.Unprotect(value)
               ?? throw new InvalidOperationException("The auth cookie could not be unprotected.");
    }
}
