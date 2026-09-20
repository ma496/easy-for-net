namespace Backend.Tests.Features.Identity.Endpoints.Account;

using System.Reflection;
using Backend.Features.Tenancy.Core;
using Backend.Features.Identity.Core.Entities;
using Backend.Features.Identity.Endpoints.Account;
using Backend.Features.Identity.Endpoints.Users;
using Backend.Tests.Features.Tenancy;

/// <summary>
/// Tests that authenticating is a platform-wide act needing no tenant, that a tenant may be named
/// beside the credentials and is then honoured or refused rather than silently ignored, and that a
/// globally deactivated account is refused authentication without its memberships being touched - so
/// that deactivating an account withholds access to every tenant at once while reactivating it
/// restores exactly what the account already held (AC-048, AC-049, AC-104, AC-105).
/// </summary>
/// <remarks>
/// <para>
/// One account is one identity across the whole platform, so the credentials name no tenant and the
/// tenant a session starts in is decided after they are accepted - from the account's memberships, or
/// from the tenant the request named. That is what makes global deactivation expressible at all:
/// there is exactly one place authentication is refused, and a membership is never the thing that
/// decides it.
/// </para>
/// <para>
/// The refusal is answered as a bad request carrying <c>userNotActive</c>, exactly as every other
/// rejection of the credentials on this endpoint is - the account is not out of service because a
/// session went stale, and a 401 here would tell the web client to go and renew a session the caller
/// was never given. A session the account already holds keeps working until its token is replaced,
/// and the renewal is what refuses it: that is where deactivation reaches a live session, and it is
/// under test below.
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

        var identifier = await IdentifierOfAsync(firstTenantId);

        (await AuthenticateAsync(account.Username, identifier)).Should().Be(HttpStatusCode.OK,
            "the credentials are the account's own and the account is in service");

        // A live session, so that what deactivation does to one can be shown alongside what it does to
        // a fresh sign-in.
        var session = await SessionForAsync(account.Username, firstTenantId);

        var (admitted, _) = await session.Client
            .GETAsync<UserListEndpoint, UserListRequest, UserListResponse>(new());

        admitted.StatusCode.Should().Be(HttpStatusCode.OK,
            "the account belongs to the tenant it acts in and holds the permission the call needs there");

        await SetActiveAsync(account.Id, isActive: false);

        // The credentials are unchanged and would still be accepted by the password check; what
        // refuses them is the account being out of service.
        var (refused, refusal) = await Client
            .POSTAsync<TokenEndpoint, TokenRequest, ProblemDetails>(
                new() { Username = account.Username, Password = TestUsers.DefaultPassword, TenantIdentifier = identifier });

        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "the caller is anonymous and is told their credentials were not accepted, not sent to renew a session they were never given");
        refusal.Errors.Should().ContainSingle();
        refusal.Errors.First().Code.Should().Be(ErrorCodes.UserNotActive);

        // The session already issued is not ended in flight - it is trusted until its token is
        // replaced - and the renewal is where deactivation reaches it: the caller cannot get a new
        // token, so the session ends when the one it holds expires.
        var (renewal, renewalRefusal) = await App.CreateClient(new ClientOptions { HandleCookies = false })
            .POSTAsync<FastEndpoints.Security.TokenRequest, ProblemDetails>(
                "api/account/refresh-token",
                new() { UserId = account.Id.ToString(), RefreshToken = session.RefreshToken });

        renewal.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "an account out of service is given no further tokens, whatever memberships it holds");
        renewalRefusal.Errors.Should().ContainSingle();
        renewalRefusal.Errors.First().Code.Should().Be(ErrorCodes.UserNotActive);

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
        var (account, firstTenantId) = await CreateAccountOfTwoTenantsAsync();
        var memberships = await MembershipIdsAsync(account.Id);

        memberships.Should().HaveCount(2);

        await SetActiveAsync(account.Id, isActive: false);

        ClearAuthToken();

        var identifier = await IdentifierOfAsync(firstTenantId);

        (await AuthenticateAsync(account.Username, identifier)).Should().Be(HttpStatusCode.BadRequest,
            "the account is out of service, whatever memberships it holds");

        await SetActiveAsync(account.Id, isActive: true);

        (await AuthenticateAsync(account.Username, identifier)).Should().Be(HttpStatusCode.OK,
            "reactivation restores the account's access - the credentials it already had are accepted again");

        (await MembershipIdsAsync(account.Id)).Should().Equal(memberships,
            "what is restored is what the account already held: nothing was added to it and nothing was taken away");
    }

    /// <summary>
    /// Verifies that authentication needs the account's own credentials and nothing else, so that a
    /// person signs in once however many tenants they belong to (AC-048, AC-049). Naming a tenant is
    /// available but never required, and the field carrying it is the only one that has been added to
    /// what a caller may state.
    /// </summary>
    [Fact]
    public async Task Sign_In_Needs_No_Tenant()
    {
        var declared = typeof(TokenRequest)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(property => property.Name)
            .ToList();

        declared.Should().BeEquivalentTo(
            [nameof(TokenRequest.IsEmail), nameof(TokenRequest.Username), nameof(TokenRequest.Email), nameof(TokenRequest.Password), nameof(TokenRequest.TenantIdentifier)],
            "the credentials are what they were, and the one field beside them names a tenant to start in rather than a second credential");

        // The account holds one membership, so the tenant the session starts in is settled without the
        // caller having said anything about it: what the credentials state is who they are.
        var tenant = await CreateTenantAsync();
        var account = await CreateTenantUserAsync(
            tenant.Id, await CreateTenantRoleAsync(tenant.Id, Allow.User_View));

        var request = new TokenRequest { Username = account.Username, Password = TestUsers.DefaultPassword };

        request.TenantIdentifier.Should().BeNull("naming a tenant is the caller's option, not something they must supply");

        typeof(TokenRequest).GetProperties()
            .Where(property => property.GetValue(request) is not null && property.Name != nameof(TokenRequest.IsEmail))
            .Select(property => property.Name)
            .Should().BeEquivalentTo([nameof(TokenRequest.Username), nameof(TokenRequest.Password)],
                "only the two credential fields are stated, and the call is still answered");

        ClearAuthToken();

        var (response, result) = await Client
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
    /// Verifies that an account belonging to several tenants starts its session inside the one it
    /// named, and that naming one is what it must do: with the field left empty there is nothing to
    /// resolve, so the sign-in is refused and the caller is asked which tenant they meant.
    /// </summary>
    [Fact]
    public async Task Naming_A_Tenant_Starts_The_Session_In_It()
    {
        var (account, firstTenantId) = await CreateAccountOfTwoTenantsAsync();
        var identifier = await IdentifierOfAsync(firstTenantId);

        ClearAuthToken();

        var (unnamed, refusal) = await Client
            .POSTAsync<TokenEndpoint, TokenRequest, ProblemDetails>(
                new() { Username = account.Username, Password = TestUsers.DefaultPassword });

        unnamed.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "two memberships settle nothing by themselves, so the question is put back to the caller rather than answered by a guess");
        refusal.Errors.Should().ContainSingle();
        refusal.Errors.First().Code.Should().Be(ErrorCodes.TenantRequired);

        var named = await AuthenticatedTenantAsync(account.Username, identifier);

        named.Should().Be(firstTenantId, "the session starts in the tenant the caller named");
    }

    /// <summary>
    /// Verifies that the identifier is matched the way it is stored rather than the way it was typed,
    /// so that a person who capitalised it or left a space around it is signed in rather than told
    /// their tenant does not exist.
    /// </summary>
    [Fact]
    public async Task Named_Tenant_Is_Found_However_It_Was_Typed()
    {
        var (account, firstTenantId) = await CreateAccountOfTwoTenantsAsync();
        var identifier = await IdentifierOfAsync(firstTenantId);

        ClearAuthToken();

        var typedLoosely = "  " + identifier.ToUpperInvariant() + "  ";

        (await AuthenticatedTenantAsync(account.Username, typedLoosely))
            .Should().Be(firstTenantId, "the identifier is compared in its normalized form, which is how it is stored");
    }

    /// <summary>
    /// Verifies that a tenant the account cannot start a session in refuses the sign-in outright,
    /// rather than quietly starting a session somewhere else or nowhere: a person who named a tenant
    /// asked for that tenant, and each way it can fail is answered with its own code.
    /// </summary>
    [Fact]
    public async Task Named_Tenant_That_Cannot_Be_Entered_Refuses_The_Sign_In()
    {
        var (account, _) = await CreateAccountOfTwoTenantsAsync();

        var stranger = await CreateTenantAsync();
        var suspended = await CreateTenantAsync(TenantStatus.Suspended);
        await MembershipService.AddAsync(suspended.Id, account.Id, [], TestContext.Current.CancellationToken);

        ClearAuthToken();

        (await RefusalCodeAsync(account.Username, "no-tenant-is-called-this"))
            .Should().Be(ErrorCodes.TenantNotFound, "an identifier that names nothing names nothing, and says no more than that");

        (await RefusalCodeAsync(account.Username, stranger.Identifier))
            .Should().Be(ErrorCodes.NotTenantMember, "the tenant exists and the account has no standing in it");

        (await RefusalCodeAsync(account.Username, suspended.Identifier))
            .Should().Be(ErrorCodes.TenantSuspended, "a suspended tenant is out of service, so there is nothing to sign in and do there");
    }

    /// <summary>
    /// Verifies that a platform administrator may name any active tenant and start inside it holding
    /// no membership there - which is how they reach a tenant that has reported a problem, without one
    /// of its own members having to sign in for them.
    /// </summary>
    [Fact]
    public async Task Platform_Administrator_May_Name_A_Tenant_They_Do_Not_Belong_To()
    {
        var tenant = await CreateTenantAsync();
        var account = await CreateAccountWithoutMembershipAsync();
        await UserService.AssignRoleAsync(account.Id, TestRoles.PlatformAdminRoleId);
        await MarkAsPlatformAccountAsync(account.Id);

        ClearAuthToken();

        (await AuthenticatedTenantAsync(account.Username, tenant.Identifier))
            .Should().Be(tenant.Id, "platform administration belongs to no tenant and so holds in all of them");

        var suspended = await CreateTenantAsync(TenantStatus.Suspended);

        (await RefusalCodeAsync(account.Username, suspended.Identifier))
            .Should().Be(ErrorCodes.TenantSuspended,
                "a suspended tenant is out of service for everybody - reaching every tenant is not reaching one that is not in service");
    }

    /// <summary>
    /// Signs in, optionally naming a tenant, and reports the tenant the session ended up acting in.
    /// </summary>
    /// <param name="username">The account to authenticate as.</param>
    /// <param name="tenantIdentifier">The tenant to name, or <see langword="null"/> to name none.</param>
    /// <returns>The tenant the session acts in, or <see langword="null"/> for none.</returns>
    private async Task<Guid?> AuthenticatedTenantAsync(string username, string? tenantIdentifier)
    {
        var (response, result) = await Client
            .POSTAsync<TokenEndpoint, TokenRequest, TokenResponse>(
                new() { Username = username, Password = TestUsers.DefaultPassword, TenantIdentifier = tenantIdentifier });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var client = App.CreateClient(new ClientOptions { HandleCookies = false });
        TestsHelper.SetAuthToken(client, result.AccessToken);

        var (_, info) = await client.GETAsync<GetInfoEndpoint, UserGetInfoResponse>();
        return info.ActiveTenantId;
    }

    /// <summary>
    /// Signs in naming a tenant that is expected to be refused, and reports the single error code the
    /// refusal carries.
    /// </summary>
    /// <param name="username">The account to authenticate as.</param>
    /// <param name="tenantIdentifier">The tenant to name.</param>
    /// <returns>The code the refusal was reported with.</returns>
    private async Task<string?> RefusalCodeAsync(string username, string tenantIdentifier)
    {
        var (response, refusal) = await Client
            .POSTAsync<TokenEndpoint, TokenRequest, ProblemDetails>(
                new() { Username = username, Password = TestUsers.DefaultPassword, TenantIdentifier = tenantIdentifier });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        refusal.Errors.Should().ContainSingle();
        return refusal.Errors.First().Code;
    }

    /// <summary>
    /// The identifier a tenant is addressed by, read back so a test can name the tenant the way a
    /// person signing in would.
    /// </summary>
    /// <param name="tenantId">The tenant whose identifier is read.</param>
    /// <returns>The tenant's identifier.</returns>
    private async Task<string> IdentifierOfAsync(Guid tenantId)
        => await DbContext.Tenants
            .AsNoTracking()
            .Where(tenant => tenant.Id == tenantId)
            .Select(tenant => tenant.Identifier)
            .SingleAsync(TestContext.Current.CancellationToken);

    /// <summary>
    /// Authenticates with the credentials alone, read as a status so that both the accepted and the
    /// refused answer can be asserted on the same call.
    /// </summary>
    /// <param name="username">The account to authenticate as.</param>
    /// <param name="tenantIdentifier">The tenant to name, or <see langword="null"/> to name none.</param>
    /// <returns>The status the call was answered with.</returns>
    private async Task<HttpStatusCode> AuthenticateAsync(string username, string? tenantIdentifier = null)
    {
        var (response, _) = await Client
            .POSTAsync<TokenEndpoint, TokenRequest, TokenResponse>(
                new() { Username = username, Password = TestUsers.DefaultPassword, TenantIdentifier = tenantIdentifier });

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
