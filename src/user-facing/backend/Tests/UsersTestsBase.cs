using Backend.Features.Identity.Core;
using Backend.Features.Identity.Core.Entities;
using Backend.Features.Identity.Endpoints.Account;

namespace Backend.Tests;

/// <summary>
/// Base class for tests that need accounts and roles of their own: a role holding exactly the
/// permissions named, an account holding exactly the roles named, and ways to act as it - on the
/// fixture's client, on a client of its own, or as a session that can be renewed.
/// </summary>
/// <remarks>
/// Every account and role made here carries a fresh name, so test classes running side by side never
/// collide and nothing depends on rows another test made.
/// </remarks>
public abstract class UsersTestsBase(App app) : AppTestsBase(app)
{
    /// <summary>The route the refresh-token service registers.</summary>
    private const string RefreshRoute = "api/account/refresh-token";

    protected IUserService UserService => Service<IUserService>();

    /// <summary>
    /// Creates a role holding exactly the permissions named, and nothing else - what lets a test prove a
    /// per-endpoint permission declaration rather than a caller's blanket authority.
    /// </summary>
    /// <param name="permissions">The permission names the role is to hold.</param>
    /// <returns>The identifier of the created role.</returns>
    protected async Task<Guid> CreateRoleAsync(params string[] permissions)
    {
        var role = new Role
        {
            SystemCreated = false,
            Name = $"Role {Guid.NewGuid():N}",
            Description = "Role made by a test"
        };
        DbContext.Roles.Add(role);
        await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var permissionIds = await DbContext.Permissions
            .Where(permission => permissions.Contains(permission.Name))
            .Select(permission => permission.Id)
            .ToListAsync(TestContext.Current.CancellationToken);

        DbContext.RolePermissions.AddRange(permissionIds.Select(permissionId => new RolePermission
        {
            RoleId = role.Id,
            PermissionId = permissionId
        }));
        await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        return role.Id;
    }

    /// <summary>
    /// Creates an active account with the shared test password holding exactly the roles named.
    /// </summary>
    /// <param name="roleIds">The roles the account holds, and no others.</param>
    /// <returns>The created account.</returns>
    protected async Task<User> CreateUserAsync(params Guid[] roleIds)
    {
        var (username, email) = NewAccountIdentity();
        var user = new User
        {
            SystemCreated = false,
            Username = username,
            Email = email
        };
        foreach (var roleId in roleIds.Distinct())
        {
            user.UserRoles.Add(new UserRole { RoleId = roleId });
        }

        return await UserService.CreateAsync(user, TestUsers.DefaultPassword);
    }

    /// <summary>
    /// Signs the fixture's client in as an account holding the shared test password.
    /// </summary>
    /// <param name="username">The account to sign in as.</param>
    protected async Task SignInAsAsync(string username)
        => await SetAuthTokenAsync(username, TestUsers.DefaultPassword);

    /// <summary>
    /// Returns a second HTTP client signed in as an account, for a test that needs two identities at
    /// once. Cookies are not carried, so the client's identity is exactly the bearer token set here.
    /// </summary>
    /// <param name="username">The account the new client is to act as.</param>
    /// <returns>A client presenting that account's bearer token and nothing else.</returns>
    protected async Task<HttpClient> ClientForAsync(string username)
    {
        var client = App.CreateClient(new ClientOptions { HandleCookies = false });
        await TestsHelper.SetNewAuthTokenAsync(client, username, TestUsers.DefaultPassword);
        return client;
    }

    /// <summary>
    /// Signs an account in on a client of its own and keeps the refresh token beside it, so the test
    /// can renew that session rather than only make requests with it.
    /// </summary>
    /// <param name="username">The account to sign in as.</param>
    /// <returns>The session, holding the client and the credential its renewal is taken with.</returns>
    protected async Task<RenewableSession> SessionForAsync(string username)
    {
        var client = App.CreateClient(new ClientOptions { HandleCookies = false });

        var (response, signedIn) = await client.POSTAsync<TokenEndpoint, Backend.Features.Identity.Endpoints.Account.TokenRequest, TokenResponse>(new()
        {
            Username = username,
            Password = TestUsers.DefaultPassword
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK, "a test that signs an account in must actually have signed it in");

        TestsHelper.SetAuthToken(client, signedIn.AccessToken);

        return new RenewableSession(App, client, Guid.Parse(signedIn.UserId), signedIn.RefreshToken);
    }

    /// <summary>
    /// A signed-in caller together with the refresh token its session was issued, so a test can renew
    /// the session the way a real client does and observe what the renewal picks up.
    /// </summary>
    /// <param name="app">The application fixture, for the anonymous client a renewal is taken on.</param>
    /// <param name="client">The client presenting this session's access token.</param>
    /// <param name="userId">The account the session belongs to.</param>
    /// <param name="refreshToken">The refresh token the session currently holds.</param>
    protected sealed class RenewableSession(App app, HttpClient client, Guid userId, string refreshToken)
    {
        /// <summary>Gets the client presenting this session's access token.</summary>
        public HttpClient Client { get; } = client;

        /// <summary>Gets the account the session belongs to.</summary>
        public Guid UserId { get; } = userId;

        /// <summary>Gets the refresh token the session currently holds; a renewal replaces it.</summary>
        public string RefreshToken { get; private set; } = refreshToken;

        /// <summary>
        /// Renews the session and leaves the client presenting the token the renewal issued.
        /// </summary>
        /// <returns>The access token the renewal issued.</returns>
        public async Task<string> RenewAsync()
        {
            // No bearer token: the refresh token is the credential, so the renewal is an anonymous request.
            var anonymous = app.CreateClient(new ClientOptions { HandleCookies = false });

            var (response, renewed) = await anonymous
                .POSTAsync<FastEndpoints.Security.TokenRequest, TokenResponse>(
                    RefreshRoute,
                    new() { UserId = UserId.ToString(), RefreshToken = RefreshToken });

            response.StatusCode.Should().Be(HttpStatusCode.OK, "the session is renewed rather than ended: the account is still who it was");

            RefreshToken = renewed.RefreshToken;
            TestsHelper.SetAuthToken(Client, renewed.AccessToken);

            return renewed.AccessToken;
        }
    }

    /// <summary>
    /// A username and the email built from it that no run of the suite can collide with: the test
    /// database is neither wiped nor recreated between runs.
    /// </summary>
    /// <returns>The username, and the email address belonging to it.</returns>
    protected static (string Username, string Email) NewAccountIdentity()
    {
        var username = $"u{Guid.NewGuid():N}";
        return (username, $"{username}@example.com");
    }
}
