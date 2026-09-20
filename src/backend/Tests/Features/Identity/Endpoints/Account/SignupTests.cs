namespace Backend.Tests.Features.Identity.Endpoints.Account;

using System.Text.Json;
using Backend.Features.Tenancy.Core;
using Backend.Features.Identity.Core;
using Backend.Features.Identity.Core.Entities;
using Backend.Features.Identity.Endpoints.Account;
using Backend.Features.Identity.Endpoints.Users;
using Backend.Tests.Features.Tenancy;

/// <summary>
/// Tests what self-service sign-up produces: an account, the tenant it works in, and the standing of
/// that tenant's administrator - created as one act, so that neither survives the other's failure.
/// </summary>
/// <remarks>
/// <para>
/// Sign-up is the one door into the application that is opened from outside every tenant, and what it
/// hands out is a tenant of one's own. The authority that comes with it is that tenant's alone: the
/// platform tier belongs to the platform's own administrators and no anonymous surface can confer it.
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
    /// Verifies that signing up creates the tenant alongside the account and places the account
    /// inside it as its only membership.
    /// </summary>
    [Fact]
    public async Task Creates_The_Account_And_Its_Tenant()
    {
        var signup = await SignUpAsync();

        signup.Account.Username.Should().Be(signup.Username);
        signup.Account.Email.Should().Be($"{signup.Username}@example.com");
        signup.Account.IsActive.Should().BeTrue("the account is created in service, which is what lets it sign in at once");
        signup.Account.IsPlatform.Should().BeFalse(
            "the platform tier is never handed out by signing up, which is anonymous and self-service");

        var tenant = await DbContext.Tenants
            .AsNoTracking()
            .AcrossAllTenants()
            .SingleAsync(candidate => candidate.IdentifierNormalized == signup.TenantIdentifier, TestContext.Current.CancellationToken);

        tenant.Name.Should().Be(signup.TenantName);
        tenant.Status.Should().Be(TenantStatus.Active, "a tenant is born in service");
        tenant.SystemCreated.Should().BeFalse("only the seeder's bootstrap tenant is system-created");

        var memberships = await DbContext.TenantMemberships
            .AcrossAllTenants()
            .AsNoTracking()
            .Where(membership => membership.UserId == signup.Account.Id)
            .Select(membership => membership.TenantId)
            .ToListAsync(TestContext.Current.CancellationToken);

        memberships.Should().ContainSingle().Which.Should().Be(tenant.Id,
            "signing up joins the tenant it created and no other - not the bootstrap tenant, not anybody else's");
    }

    /// <summary>
    /// Verifies that self-service sign-up completes normally when no tenant context is established,
    /// which is the only state an anonymous visitor can arrive in.
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
                ConfirmPassword = SignupPassword,
                TenantName = $"Tenant {Guid.NewGuid():N}",
                TenantIdentifier = NewTenantIdentifier()
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "sign-up is account self-service, so the one answer it must never give is a refusal for want of a tenant");

        var created = await DbContext.Users
            .AsNoTracking()
            .AnyAsync(user => user.UsernameNormalized == username, TestContext.Current.CancellationToken);

        created.Should().BeTrue("the account was created rather than the request being turned away");
    }

    /// <summary>
    /// Verifies that the account signing up administers the tenant it created, and that the authority
    /// it gains reaches nothing platform-wide.
    /// </summary>
    [Fact]
    public async Task Administers_Its_Own_Tenant_And_Holds_No_Platform_Permission()
    {
        var signup = await SignUpAsync();
        var client = await SignedInClientAsync(signup.Username, signup.Password);

        var (infoResponse, info) = await client.GETAsync<GetInfoEndpoint, UserGetInfoResponse>();

        infoResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        info.Tenants.Should().ContainSingle("the account belongs to the one tenant it created");
        info.ActiveTenant!.Identifier.Should().Be(signup.TenantIdentifier,
            "sign-in resolves that single membership without being told which tenant is meant");
        info.IsPlatform.Should().BeFalse(
            "administering a tenant is not the same standing as belonging to the platform tier");

        var platformPermissions = PlatformOnlyPermissionNames();

        platformPermissions.Should().NotBeEmpty(
            "the catalogue declares which permissions are platform level, and the comparison below is only meaningful if it declares some");

        PermissionClaimsOf(AccessTokenOf(client)).Should().NotBeEmpty(
                "the account administers the tenant it created, so its session carries that tenant's authority")
            .And.NotIntersectWith(platformPermissions,
                "the authority sign-up confers is the new tenant's own and reaches nothing platform-wide");

        info.Roles.SelectMany(role => role.Permissions).Select(permission => permission.Name)
            .Should().NotIntersectWith(platformPermissions,
                "every permission held comes through the new tenant's own role, and a tenant-tier role carries no platform permission");

        // The standing is usable rather than merely reported: a tenant-scoped call the account would
        // have been refused without a tenant is answered inside the one it just created.
        var (admitted, _) = await client
            .GETAsync<UserListEndpoint, UserListRequest, UserListResponse>(new());

        admitted.StatusCode.Should().Be(HttpStatusCode.OK,
            "the account is a member of its tenant and administers it from its very first request");
    }

    /// <summary>
    /// Verifies that a tenant identifier already in use is refused against the field that carries it,
    /// rather than surfacing as a database error from the unique index behind it.
    /// </summary>
    [Fact]
    public async Task Refuses_A_Tenant_Identifier_Already_In_Use()
    {
        var taken = await SignUpAsync();

        var username = $"signup-{Guid.NewGuid():N}";
        var visitor = App.CreateClient(new ClientOptions { HandleCookies = false });

        var (response, problem) = await visitor
            .POSTAsync<SignupEndpoint, SignupRequest, ProblemDetails>(new()
            {
                Username = username,
                Email = $"{username}@example.com",
                Password = SignupPassword,
                ConfirmPassword = SignupPassword,
                TenantName = $"Tenant {Guid.NewGuid():N}",
                TenantIdentifier = taken.TenantIdentifier
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.Should().ContainSingle()
            .Which.Code.Should().Be(ErrorCodes.TenantIdentifierAlreadyExists);

        var created = await DbContext.Users
            .AsNoTracking()
            .AnyAsync(user => user.UsernameNormalized == username, TestContext.Current.CancellationToken);

        created.Should().BeFalse("nothing is persisted when the tenant cannot be created");
    }

    /// <summary>
    /// Verifies that the account and the tenant are created as one act: a sign-up refused for the
    /// tenant leaves no account behind for the person to be stuck with.
    /// </summary>
    /// <remarks>
    /// The tenant is refused here for a reason the endpoint cannot see coming - a name that passes
    /// validation and an identifier taken between the check and the write - which is arranged by
    /// reusing an identifier that already exists. What is asserted is the absence of the account: the
    /// endpoint's own guard and the unique index behind it both roll the whole transaction back.
    /// </remarks>
    [Fact]
    public async Task A_Refused_Tenant_Leaves_No_Account_Behind()
    {
        var taken = await SignUpAsync();

        var username = $"signup-{Guid.NewGuid():N}";
        var email = $"{username}@example.com";
        var visitor = App.CreateClient(new ClientOptions { HandleCookies = false });

        await visitor.POSTAsync<SignupEndpoint, SignupRequest, ProblemDetails>(new()
        {
            Username = username,
            Email = email,
            Password = SignupPassword,
            ConfirmPassword = SignupPassword,
            TenantName = $"Tenant {Guid.NewGuid():N}",
            TenantIdentifier = taken.TenantIdentifier
        });

        var accountExists = await DbContext.Users
            .AsNoTracking()
            .AnyAsync(user => user.UsernameNormalized == username, TestContext.Current.CancellationToken);

        accountExists.Should().BeFalse(
            "the account and its tenant are one act, so a failure that costs the tenant costs the account too");

        // And the name is still free, which is the point of rolling back rather than leaving a
        // half-created account: the person can sign up again with the same details.
        var retryIdentifier = NewTenantIdentifier();
        var (retry, _) = await visitor
            .POSTAsync<SignupEndpoint, SignupRequest, SignupResponse>(new()
            {
                Username = username,
                Email = email,
                Password = SignupPassword,
                ConfirmPassword = SignupPassword,
                TenantName = $"Tenant {Guid.NewGuid():N}",
                TenantIdentifier = retryIdentifier
            });

        retry.StatusCode.Should().Be(HttpStatusCode.OK, "the failed attempt reserved neither the username nor the email");
    }

    /// <summary>
    /// Signs a visitor up through the endpoint, so the account and tenant under test are the ones the
    /// production path creates rather than rows assembled by the test.
    /// </summary>
    /// <returns>The stored account, the credentials it signed up with, and the tenant it created.</returns>
    private async Task<(User Account, string Username, string Password, string TenantName, string TenantIdentifier)> SignUpAsync()
    {
        var username = $"signup-{Guid.NewGuid():N}";
        var tenantName = $"Tenant {Guid.NewGuid():N}";
        var tenantIdentifier = NewTenantIdentifier();
        var visitor = App.CreateClient(new ClientOptions { HandleCookies = false });

        var (response, _) = await visitor
            .POSTAsync<SignupEndpoint, SignupRequest, SignupResponse>(new()
            {
                Username = username,
                Email = $"{username}@example.com",
                Password = SignupPassword,
                ConfirmPassword = SignupPassword,
                TenantName = tenantName,
                TenantIdentifier = tenantIdentifier
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK, "sign-up is answered without a tenant");

        var account = await DbContext.Users
            .AsNoTracking()
            .SingleAsync(user => user.UsernameNormalized == username, TestContext.Current.CancellationToken);

        return (account, username, SignupPassword, tenantName, tenantIdentifier);
    }

    /// <summary>
    /// A client presenting the signed-up account's own credentials, for the cases that go on to ask
    /// what that account may do. No tenant is named: the account holds exactly one membership, so
    /// sign-in resolves it without being told.
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
