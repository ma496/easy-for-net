namespace Backend.Tests.Features.Identity;

using Backend.Features.Identity.Core;
using Backend.Features.Identity.Core.Entities;
using Backend.Features.Identity.Endpoints.Account;
using Backend.Tests.Features.Tenancy;

/// <summary>
/// Base class for the suites that show a change to an account, its password or a role's grants ending the
/// live sessions it invalidates, and only those.
/// </summary>
/// <remarks>
/// A session is "ended" when both halves of it are gone: its access token answers 401 on the very next
/// request, without any renewal, and its refresh token is refused. A session is "alive" when its token still
/// works and a renewal still succeeds. The assertions below say exactly that, so each test states which
/// sessions a change names and which it does not.
/// </remarks>
public abstract class SessionRevocationTestsBase(App app) : TenancyTestsBase(app)
{
    private const string RefreshRoute = "api/account/refresh-token";

    /// <summary>
    /// The status a request for the caller's own account information answers with, made by the client as
    /// it stands - the access token in hand, never renewed first.
    /// </summary>
    /// <param name="client">The client presenting the session's access token.</param>
    /// <returns>The response status.</returns>
    protected static async Task<HttpStatusCode> InfoStatusAsync(HttpClient client)
        => (await client.GETAsync<GetInfoEndpoint, ProblemDetails>()).Response.StatusCode;

    /// <summary>
    /// Posts the session's refresh token to the refresh endpoint on an anonymous client and returns what it
    /// answered, without asserting on it. A successful refresh consumes the token, so a test that goes on
    /// using the session after a successful call here must use <c>RenewAsync</c> instead.
    /// </summary>
    /// <param name="session">The session whose refresh token is presented.</param>
    /// <returns>The response status.</returns>
    protected async Task<HttpStatusCode> RefreshStatusAsync(RenewableSession session)
    {
        var anonymous = App.CreateClient(new ClientOptions { HandleCookies = false });

        var (response, _) = await anonymous.POSTAsync<FastEndpoints.Security.TokenRequest, ProblemDetails>(
            RefreshRoute,
            new() { UserId = session.UserId.ToString(), RefreshToken = session.RefreshToken });

        return response.StatusCode;
    }

    /// <summary>
    /// Asserts a session has ended: its access token, not renewed, answers 401 and its refresh token is refused.
    /// </summary>
    /// <param name="session">The session that must be gone.</param>
    protected async Task AssertEndedAsync(RenewableSession session)
    {
        (await InfoStatusAsync(session.Client)).Should().Be(HttpStatusCode.Unauthorized,
            "the session was deleted from the store, so its access token names nothing");
        (await RefreshStatusAsync(session)).Should().Be(HttpStatusCode.Unauthorized,
            "the refresh-token row went with the session, so nothing can renew it");
    }

    /// <summary>
    /// Asserts a session is untouched: its access token works, and so does a renewal, and so does the token
    /// the renewal issued.
    /// </summary>
    /// <param name="session">The session that must still work.</param>
    protected async Task AssertAliveAsync(RenewableSession session)
    {
        (await InfoStatusAsync(session.Client)).Should().Be(HttpStatusCode.OK, "nothing ended this session");

        await session.RenewAsync();

        (await InfoStatusAsync(session.Client)).Should().Be(HttpStatusCode.OK, "and its refresh token still renews it");
    }

    /// <summary>
    /// The identifier of the stored session an access token names.
    /// </summary>
    /// <param name="session">The session whose current access token is read.</param>
    /// <returns>The session identifier.</returns>
    protected static string SessionIdOf(RenewableSession session)
        => TestsHelper.PayloadOf(session.Client.DefaultRequestHeaders.Authorization!.Parameter!)[ClaimConstants.SessionId].GetString()!;

    /// <summary>
    /// Creates a role belonging to the platform - to no tenant - and holding no permission, for a test that
    /// changes what a platform account holds without touching any seeded role.
    /// </summary>
    /// <returns>The identifier of the created role.</returns>
    protected async Task<Guid> CreatePlatformRoleAsync()
    {
        using var platformScope = TenantContext.BeginPlatformScope();

        var role = new Role
        {
            SystemCreated = false,
            Name = $"Platform role {Guid.NewGuid():N}",
            Description = "Platform role made by a session revocation test"
        };
        DbContext.Roles.Add(role);
        await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        return role.Id;
    }

    /// <summary>
    /// The stored identifier of a permission, so a test can grant by identifier as the request does.
    /// </summary>
    /// <param name="permissionName">The permission's name.</param>
    /// <returns>The permission's identifier.</returns>
    protected async Task<Guid> PermissionIdAsync(string permissionName)
        => await DbContext.Permissions
            .AsNoTracking()
            .Where(permission => permission.Name == permissionName)
            .Select(permission => permission.Id)
            .SingleAsync(TestContext.Current.CancellationToken);
}
