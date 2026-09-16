namespace Backend.Tests.Features.Identity.Endpoints.Account;

using System.Reflection;
using Backend.Data.Entities;
using Backend.Features.Identity.Core.Entities;
using Backend.Features.Identity.Endpoints.Account;
using Backend.Features.Identity.Endpoints.Users;
using Backend.Tests.Features.Tenancy;

/// <summary>
/// Tests that authenticating is a platform-wide act that names no tenant, and that a globally
/// deactivated account is refused it without its memberships being touched - so that deactivating an
/// account withholds access to every tenant at once while reactivating it restores exactly what the
/// account already held (AC-048, AC-049, AC-104, AC-105).
/// </summary>
/// <remarks>
/// <para>
/// One account is one identity across the whole platform, so the credentials carry no tenant and the
/// tenant a session starts in is resolved from the account's memberships after the credentials are
/// accepted. That is what makes global deactivation expressible at all: there is exactly one place
/// authentication is refused, and a membership is never the thing that decides it.
/// </para>
/// <para>
/// The refusal is answered as a bad request carrying <c>userNotActive</c>, exactly as every other
/// rejection of the credentials on this endpoint is - the account is not out of service because a
/// session went stale, and a 401 here would tell the web client to go and renew a session the caller
/// was never given. What a deactivated account's <em>existing</em> session gets is a different
/// answer, and that one is under test below.
/// </para>
/// <para>
/// The account under test is made by the test rather than taken from the seed, because the point of
/// both deactivation cases is that the memberships survive untouched: they are captured by identifier
/// before the account is deactivated and compared afterwards, on rows this test created and nothing
/// else can be writing to.
/// </para>
/// </remarks>
public class TokenTests(App app) : TenancyTestsBase(app)
{
    /// <summary>
    /// Verifies that a deactivated account cannot authenticate, however many tenants it belongs to,
    /// and that refusing it leaves every membership it holds exactly where it was (AC-104).
    /// </summary>
    [Fact]
    public async Task Deactivated_Account_Cannot_Authenticate()
    {
        var (account, firstTenantId) = await CreateAccountOfTwoTenantsAsync();
        var memberships = await MembershipIdsAsync(account.Id);

        memberships.Should().HaveCount(2,
            "the account belongs to two tenants, which is what makes 'irrespective of the memberships it holds' mean something");

        ClearAuthToken();

        (await AuthenticateAsync(account.Username)).Should().Be(HttpStatusCode.OK,
            "the credentials are the account's own and the account is in service");

        // The token is minted while the account is still in service and is never replaced, so what the
        // tenant-scoped call below is refused with can only be the deactivation.
        var client = await ClientForAsync(account.Username, firstTenantId);

        var (admitted, _) = await client
            .GETAsync<UserListEndpoint, UserListRequest, UserListResponse>(new());

        admitted.StatusCode.Should().Be(HttpStatusCode.OK,
            "the account belongs to the tenant it acts in and holds the permission the call needs there");

        await SetActiveAsync(account.Id, isActive: false);

        // The credentials are unchanged and would still be accepted by the password check; what
        // refuses them is the account being out of service.
        var (refused, refusal) = await App.Client
            .POSTAsync<TokenEndpoint, TokenRequest, ProblemDetails>(
                new() { Username = account.Username, Password = TestUsers.DefaultPassword });

        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "the caller is anonymous and is told their credentials were not accepted, not sent to renew a session they were never given");
        refusal.Errors.Should().ContainSingle();
        refusal.Errors.First().Code.Should().Be(ErrorCodes.UserNotActive);

        // Every tenant-scoped operation is refused too - including the one the token issued a moment
        // ago was admitted for. That is the point of deactivating an account globally rather than
        // tenant by tenant: the memberships are all still there and none of them admits it.
        var (refusedWork, _) = await client
            .GETAsync<UserListEndpoint, UserListRequest, UserListResponse>(new());

        refusedWork.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "the session belongs to an account that is out of service, so the caller is nobody before any tenant is considered");

        (await MembershipIdsAsync(account.Id)).Should().Equal(memberships,
            "deactivating an account withholds its access, it does not take it out of its tenants - the memberships wait untouched for it to be reactivated");
    }

    /// <summary>
    /// Verifies that reactivating a deactivated account restores it to exactly the memberships it
    /// already held, without it having been added to or removed from any tenant in the meantime
    /// (AC-105).
    /// </summary>
    [Fact]
    public async Task Reactivated_Account_Keeps_Exactly_Its_Memberships()
    {
        var (account, _) = await CreateAccountOfTwoTenantsAsync();
        var memberships = await MembershipIdsAsync(account.Id);

        memberships.Should().HaveCount(2);

        await SetActiveAsync(account.Id, isActive: false);

        ClearAuthToken();

        (await AuthenticateAsync(account.Username)).Should().Be(HttpStatusCode.BadRequest,
            "the account is out of service, whatever memberships it holds");

        await SetActiveAsync(account.Id, isActive: true);

        (await AuthenticateAsync(account.Username)).Should().Be(HttpStatusCode.OK,
            "reactivation restores the account's access - the credentials it already had are accepted again");

        (await MembershipIdsAsync(account.Id)).Should().Equal(memberships,
            "what is restored is what the account already held: nothing was added to it and nothing was taken away");
    }

    /// <summary>
    /// Verifies that authentication asks for the account's own credentials and nothing else, so that
    /// a person signs in once however many tenants they belong to (AC-048, AC-049).
    /// </summary>
    [Fact]
    public async Task Sign_In_Does_Not_Name_A_Tenant()
    {
        var declared = typeof(TokenRequest)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(property => property.Name)
            .ToList();

        declared.Should().NotContain(name => name.Contains("tenant", StringComparison.OrdinalIgnoreCase),
            "the tenant a session acts in is resolved from the account's memberships, never supplied alongside the credentials");
        declared.Should().BeEquivalentTo([nameof(TokenRequest.IsEmail), nameof(TokenRequest.Username), nameof(TokenRequest.Email), nameof(TokenRequest.Password)],
            "these four are the whole of what a caller states to authenticate, and no fifth field has appeared to carry a tenant");

        // The account holds one membership, so the tenant the session starts in is settled without the
        // caller having said anything about it: what the credentials state is who they are.
        var tenant = await CreateTenantAsync();
        var account = await CreateTenantUserAsync(
            tenant.Id, await CreateTenantRoleAsync(tenant.Id, Allow.User_View));

        var request = new TokenRequest { Username = account.Username, Password = TestUsers.DefaultPassword };

        typeof(TokenRequest).GetProperties()
            .Where(property => property.GetValue(request) is not null && property.Name != nameof(TokenRequest.IsEmail))
            .Select(property => property.Name)
            .Should().BeEquivalentTo([nameof(TokenRequest.Username), nameof(TokenRequest.Password)],
                "only the two credential fields are stated, and the call is still answered");

        ClearAuthToken();

        var (response, result) = await App.Client
            .POSTAsync<TokenEndpoint, TokenRequest, TokenResponse>(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "a username and a password are the whole of what signing in requires");
        result.AccessToken.Should().NotBeNullOrWhiteSpace();
        result.RefreshToken.Should().NotBeNullOrWhiteSpace();

        // The identity the token was issued for is the account's own, and the tenant it resolved is
        // the account's single membership - resolved from the memberships, since the request named none.
        var client = App.CreateClient(new ClientOptions { HandleCookies = false });
        TestsHelper.SetAuthToken(client, result.AccessToken);

        var (infoResponse, info) = await client.GETAsync<GetInfoEndpoint, UserGetInfoResponse>();

        infoResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        info.Id.Should().Be(account.Id);
        info.ActiveTenantId.Should().Be(tenant.Id,
            "the session starts in the account's only tenant without the caller having named it");
    }

    /// <summary>
    /// Authenticates with the credentials alone, read as a status so that both the accepted and the
    /// refused answer can be asserted on the same call.
    /// </summary>
    /// <param name="username">The account to authenticate as.</param>
    /// <returns>The status the call was answered with.</returns>
    private async Task<HttpStatusCode> AuthenticateAsync(string username)
    {
        var (response, _) = await App.Client
            .POSTAsync<TokenEndpoint, TokenRequest, TokenResponse>(
                new() { Username = username, Password = TestUsers.DefaultPassword });

        return response.StatusCode;
    }

    /// <summary>
    /// An account holding an active membership in two tenants of its own, with a role in each - the
    /// standing in which the account's access to every tenant either stands or falls with the account
    /// itself, and in which sign-in resolves no tenant by itself.
    /// </summary>
    /// <returns>The created account, and the first of the two tenants it belongs to.</returns>
    private async Task<(User Account, Guid FirstTenantId)> CreateAccountOfTwoTenantsAsync()
    {
        var first = await CreateTenantAsync();
        var second = await CreateTenantAsync();

        var account = await CreateTenantUserAsync(
            first.Id, await CreateTenantRoleAsync(first.Id, Allow.User_View));

        await MembershipService.AddAsync(
            second.Id,
            account.Id,
            [await CreateTenantRoleAsync(second.Id, Allow.User_View)],
            TestContext.Current.CancellationToken);

        return (account, first.Id);
    }

    /// <summary>
    /// Puts an account into and out of service at the platform tier, which is the only place the flag
    /// is set - no tenant, and no endpoint of the tenancy feature, is involved.
    /// </summary>
    /// <param name="userId">The account to change.</param>
    /// <param name="isActive">Whether the account is to be in service.</param>
    private async Task SetActiveAsync(Guid userId, bool isActive)
    {
        var account = await DbContext.Users
            .SingleAsync(user => user.Id == userId, TestContext.Current.CancellationToken);

        account.IsActive = isActive;
        await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// The memberships an account holds right now, by identifier, so that a before-and-after
    /// comparison is exact rather than a count that a swap would satisfy.
    /// </summary>
    /// <param name="userId">The account whose memberships are read.</param>
    /// <returns>The identifiers of its live memberships, in a stable order.</returns>
    private async Task<List<Guid>> MembershipIdsAsync(Guid userId)
        => await DbContext.TenantMemberships
            .AcrossAllTenants()
            .AsNoTracking()
            .Where(membership => membership.UserId == userId)
            .OrderBy(membership => membership.Id)
            .Select(membership => membership.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
}
