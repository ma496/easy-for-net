using Backend.Features.Identity.Endpoints.Account;
using Backend.Features.Tenancy.Core;
using Backend.Features.Tenancy.Endpoints.Tenants;
using Backend.Tests.Fakes;
using Backend.Tests.Features.Identity;
using Backend.Tests.Features.Notifications.Core;
using TokenRequest = Backend.Features.Identity.Endpoints.Account.TokenRequest;

namespace Backend.Tests.Features.Tenancy.Core.Hosting;

/// <summary>
/// Tests for tenant host resolution end to end: sign-in deciding the tenant from the host, every
/// authenticated request and the notification hub refusing a session on a host it does not belong on,
/// switching and exiting refused where the host decides the scope, and an identifier change ending the
/// tenant's sessions.
/// </summary>
/// <remarks>
/// The shared host runs with resolution off. A request here turns it on for itself by carrying
/// <see cref="TenantHostingOverride.GateHeader"/>, which resolves the platform on
/// <see cref="TenantHostingOverride.PlatformHost"/> and each tenant on its identifier's host, so no other
/// class sees a host name anything. Every tenant and account is created here; no seeded tenant is changed.
/// </remarks>
[Collection("TenantHosting")]
public class TenantHostingTests(App app) : SessionRevocationTestsBase(app)
{
    /// <summary>A member signing in on its tenant's host enters that tenant, and its session records the identifier.</summary>
    [Fact]
    public async Task Member_Signs_In_On_Its_Tenant_Host()
    {
        var tenant = await CreateTenantAsync();
        var member = await CreateTenantUserAsync(tenant.Id);
        using var client = HostClient(TenantHostingOverride.TenantHost(tenant.Identifier));

        var (response, signedIn) = await SignInAsync(client, member.Username);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var session = await SessionOfAsync(signedIn.AccessToken);
        session!.TenantId.Should().Be(tenant.Id);
        session.TenantIdentifier.Should().Be(tenant.IdentifierNormalized);

        TestsHelper.SetAuthToken(client, signedIn.AccessToken);
        (await InfoStatusAsync(client)).Should().Be(HttpStatusCode.OK, "the session belongs on the host it is used on");
    }

    /// <summary>
    /// The host chooses, it never grants: an account that is not a member of the host's tenant is refused,
    /// a platform account included, and a suspended tenant is refused to its own members.
    /// </summary>
    [Fact]
    public async Task Tenant_Host_Still_Checks_Membership_And_Suspension()
    {
        var tenant = await CreateTenantAsync();
        var otherTenant = await CreateTenantAsync();
        var outsider = await CreateTenantUserAsync(otherTenant.Id);
        var platformOutsider = await CreateAccountWithoutMembershipAsync();
        await MarkAsPlatformAccountAsync(platformOutsider.Id);
        using var client = HostClient(TenantHostingOverride.TenantHost(tenant.Identifier));

        await AssertSignInRefusedAsync(client, outsider.Username, ErrorCodes.NotTenantMember);
        await AssertSignInRefusedAsync(client, platformOutsider.Username, ErrorCodes.NotTenantMember);

        var suspended = await CreateTenantAsync();
        var suspendedMember = await CreateTenantUserAsync(suspended.Id);
        await DbContext.Tenants.Where(row => row.Id == suspended.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.Status, TenantStatus.Suspended), TestContext.Current.CancellationToken);
        using var suspendedClient = HostClient(TenantHostingOverride.TenantHost(suspended.Identifier));

        await AssertSignInRefusedAsync(suspendedClient, suspendedMember.Username, ErrorCodes.TenantSuspended);

        using var unknownTenantClient = HostClient(TenantHostingOverride.TenantHost($"t-{Guid.NewGuid():N}"));
        await AssertSignInRefusedAsync(unknownTenantClient, outsider.Username, ErrorCodes.TenantNotFound);
    }

    /// <summary>
    /// A tenant named on the request beside a tenant host must be the host's tenant, compared
    /// case-insensitively; a different one is refused rather than either being picked.
    /// </summary>
    [Fact]
    public async Task Requested_Tenant_Must_Agree_With_The_Tenant_Host()
    {
        var tenant = await CreateTenantAsync();
        var otherTenant = await CreateTenantAsync();
        var member = await CreateDualTenantMemberAsync(tenant.Id, otherTenant.Id);
        using var client = HostClient(TenantHostingOverride.TenantHost(tenant.Identifier));

        await AssertSignInRefusedAsync(client, member.Username, ErrorCodes.TenantHostMismatch, otherTenant.Identifier);

        var (agreeing, signedIn) = await SignInAsync(client, member.Username, tenant.Identifier.ToUpperInvariant());
        agreeing.StatusCode.Should().Be(HttpStatusCode.OK);
        (await SessionOfAsync(signedIn.AccessToken))!.TenantId.Should().Be(tenant.Id);

        // Without naming one, the host settles which of its two tenants the account enters.
        var (unnamed, unnamedSignIn) = await SignInAsync(client, member.Username);
        unnamed.StatusCode.Should().Be(HttpStatusCode.OK);
        (await SessionOfAsync(unnamedSignIn.AccessToken))!.TenantId.Should().Be(tenant.Id);
    }

    /// <summary>
    /// On the platform host a platform account signs in with no tenant, an ordinary account is refused with
    /// <c>tenantHostRequired</c>, and a tenant named there is refused with <c>tenantHostMismatch</c>.
    /// </summary>
    [Fact]
    public async Task Platform_Host_Admits_Platform_Accounts_Only_And_In_No_Tenant()
    {
        var tenant = await CreateTenantAsync();
        var member = await CreateTenantUserAsync(tenant.Id);
        var platformMember = await CreateTenantUserAsync(tenant.Id);
        await MarkAsPlatformAccountAsync(platformMember.Id);
        using var client = HostClient(TenantHostingOverride.PlatformHost);

        var (response, signedIn) = await SignInAsync(client, platformMember.Username);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var session = await SessionOfAsync(signedIn.AccessToken);
        session!.TenantId.Should().BeNull("the platform host is platform scope, whatever memberships the account holds");
        session.TenantIdentifier.Should().BeNull();

        await AssertSignInRefusedAsync(client, member.Username, ErrorCodes.TenantHostRequired);
        await AssertSignInRefusedAsync(client, platformMember.Username, ErrorCodes.TenantHostMismatch, tenant.Identifier);
    }

    /// <summary>
    /// A host that names nothing - here one outside the test configuration - leaves sign-in exactly as it was:
    /// an account with one membership enters it, and a named tenant is honoured.
    /// </summary>
    [Fact]
    public async Task Unknown_Host_Leaves_Sign_In_Unchanged()
    {
        var tenant = await CreateTenantAsync();
        var otherTenant = await CreateTenantAsync();
        var member = await CreateTenantUserAsync(tenant.Id);
        var dualMember = await CreateDualTenantMemberAsync(tenant.Id, otherTenant.Id);
        using var client = HostClient("api.unrelated.test");

        var (single, singleSignIn) = await SignInAsync(client, member.Username);
        single.StatusCode.Should().Be(HttpStatusCode.OK);
        (await SessionOfAsync(singleSignIn.AccessToken))!.TenantId.Should().Be(tenant.Id);

        await AssertSignInRefusedAsync(client, dualMember.Username, ErrorCodes.TenantRequired);

        var (named, namedSignIn) = await SignInAsync(client, dualMember.Username, otherTenant.Identifier);
        named.StatusCode.Should().Be(HttpStatusCode.OK);
        (await SessionOfAsync(namedSignIn.AccessToken))!.TenantIdentifier.Should().Be(otherTenant.IdentifierNormalized);

        TestsHelper.SetAuthToken(client, namedSignIn.AccessToken);
        (await InfoStatusAsync(client)).Should().Be(HttpStatusCode.OK, "a host naming nothing binds no session");
    }

    /// <summary>
    /// A session is honoured on its own tenant's host alone: on another tenant's host and on the platform host
    /// an endpoint answers 403 <c>tenantHostMismatch</c>, and so does the hub upgrade - while the same
    /// session connects to the hub on its own host. A platform-scope session is refused on a tenant host.
    /// </summary>
    [Fact]
    public async Task Session_Is_Refused_On_A_Host_It_Does_Not_Belong_On()
    {
        var tenant = await CreateTenantAsync();
        var otherTenant = await CreateTenantAsync();
        var member = await CreateDualTenantMemberAsync(tenant.Id, otherTenant.Id);
        var ownHost = TenantHostingOverride.TenantHost(tenant.Identifier);
        var otherHost = TenantHostingOverride.TenantHost(otherTenant.Identifier);

        using var signInClient = HostClient(ownHost);
        var (_, signedIn) = await SignInAsync(signInClient, member.Username);
        var token = signedIn.AccessToken;

        await AssertHostMismatchAsync(otherHost, token);
        await AssertHostMismatchAsync(TenantHostingOverride.PlatformHost, token);

        await using (var refused = HubProbe.Create(App.Server, token, member.Id, tenant.Id, configureRequest: OnHost(otherHost)))
        {
            var start = () => refused.Connection.StartAsync(TestContext.Current.CancellationToken);
            (await start.Should().ThrowAsync<Exception>()).Which.Message.Should().Contain("403");
        }

        await using (var refusedOnPlatform = HubProbe.Create(App.Server, token, member.Id, tenant.Id, configureRequest: OnHost(TenantHostingOverride.PlatformHost)))
        {
            var start = () => refusedOnPlatform.Connection.StartAsync(TestContext.Current.CancellationToken);
            (await start.Should().ThrowAsync<Exception>()).Which.Message.Should().Contain("403");
        }

        // On its own host, from its own origin, the same session is admitted.
        await using var admitted = await HubProbe.ConnectAsync(App.Server, token, member.Id, tenant.Id, $"http://{ownHost}", OnHost(ownHost));

        // A platform-scope session belongs on the platform host alone.
        var platformAccount = await CreateAccountWithoutMembershipAsync();
        await MarkAsPlatformAccountAsync(platformAccount.Id);
        using var platformClient = HostClient(TenantHostingOverride.PlatformHost);
        var (_, platformSignIn) = await SignInAsync(platformClient, platformAccount.Username);
        TestsHelper.SetAuthToken(platformClient, platformSignIn.AccessToken);
        (await InfoStatusAsync(platformClient)).Should().Be(HttpStatusCode.OK);
        await AssertHostMismatchAsync(ownHost, platformSignIn.AccessToken);
    }

    /// <summary>
    /// The host check refuses only authenticated requests, and a store outage is still answered 503 ahead of it.
    /// </summary>
    [Fact]
    public async Task Anonymous_Requests_Pass_And_A_Store_Outage_Is_Still_503()
    {
        var tenant = await CreateTenantAsync();
        var otherTenant = await CreateTenantAsync();
        var member = await CreateTenantUserAsync(tenant.Id);

        using var anonymous = HostClient(TenantHostingOverride.TenantHost(otherTenant.Identifier));
        (await InfoStatusAsync(anonymous)).Should().Be(HttpStatusCode.Unauthorized, "an anonymous caller is the endpoint's to answer");

        using var signInClient = HostClient(TenantHostingOverride.TenantHost(tenant.Identifier));
        var (_, signedIn) = await SignInAsync(signInClient, member.Username);
        Service<SessionStoreFaults>().MakeUnreachable(TestsHelper.PayloadOf(signedIn.AccessToken)["sid"].GetString()!);

        using var elsewhere = HostClient(TenantHostingOverride.TenantHost(otherTenant.Identifier));
        TestsHelper.SetAuthToken(elsewhere, signedIn.AccessToken);
        var (response, refusal) = await elsewhere.GETAsync<GetInfoEndpoint, ProblemDetails>();
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        refusal.Errors.Single().Code.Should().Be(ErrorCodes.SessionStoreUnavailable.Value);
    }

    /// <summary>
    /// Where the host decides the scope, switching and exiting are refused with <c>tenantHostMismatch</c>; on a
    /// host that names nothing both work as before.
    /// </summary>
    [Fact]
    public async Task Switch_And_Exit_Are_Refused_On_A_Resolved_Host()
    {
        var tenant = await CreateTenantAsync();
        var otherTenant = await CreateTenantAsync();
        var member = await CreateDualTenantMemberAsync(tenant.Id, otherTenant.Id);
        await MarkAsPlatformAccountAsync(member.Id);

        using var tenantClient = HostClient(TenantHostingOverride.TenantHost(tenant.Identifier));
        var (_, signedIn) = await SignInAsync(tenantClient, member.Username);
        TestsHelper.SetAuthToken(tenantClient, signedIn.AccessToken);

        var (switchResponse, switchRefusal) = await tenantClient.POSTAsync<TenantSwitchEndpoint, TenantSwitchRequest, ProblemDetails>(new() { TenantId = otherTenant.Id });
        switchResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        switchRefusal.Errors.Single().Code.Should().Be(ErrorCodes.TenantHostMismatch.Value);

        var (exitResponse, exitRefusal) = await tenantClient.POSTAsync<TenantExitEndpoint, ProblemDetails>();
        exitResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        exitRefusal.Errors.Single().Code.Should().Be(ErrorCodes.TenantHostMismatch.Value);

        using var platformClient = HostClient(TenantHostingOverride.PlatformHost);
        var (_, platformSignIn) = await SignInAsync(platformClient, member.Username);
        TestsHelper.SetAuthToken(platformClient, platformSignIn.AccessToken);
        var (platformSwitch, platformRefusal) = await platformClient.POSTAsync<TenantSwitchEndpoint, TenantSwitchRequest, ProblemDetails>(new() { TenantId = tenant.Id });
        platformSwitch.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        platformRefusal.Errors.Single().Code.Should().Be(ErrorCodes.TenantHostMismatch.Value);

        // Unchanged where the host names nothing.
        using var unresolved = HostClient("api.unrelated.test");
        var (_, unresolvedSignIn) = await SignInAsync(unresolved, member.Username, tenant.Identifier);
        TestsHelper.SetAuthToken(unresolved, unresolvedSignIn.AccessToken);
        var (switched, _) = await unresolved.POSTAsync<TenantSwitchEndpoint, TenantSwitchRequest, TenantSwitchResponse>(new() { TenantId = otherTenant.Id });
        switched.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// A session records the identifier of the tenant it acts in whichever way it was minted: sign-in,
    /// refresh, and switch.
    /// </summary>
    [Fact]
    public async Task Session_Records_The_Tenant_Identifier_At_Sign_In_Refresh_And_Switch()
    {
        var tenant = await CreateTenantAsync();
        var otherTenant = await CreateTenantAsync();
        var member = await CreateDualTenantMemberAsync(tenant.Id, otherTenant.Id);

        var session = await SessionForAsync(member.Username, tenant.Id);
        var signInToken = session.Client.DefaultRequestHeaders.Authorization!.Parameter!;
        (await SessionOfAsync(signInToken))!.TenantIdentifier.Should().Be(tenant.IdentifierNormalized);

        var renewedToken = await session.RenewAsync();
        (await SessionOfAsync(renewedToken))!.TenantIdentifier.Should().Be(tenant.IdentifierNormalized);

        var switchedToken = await TestsHelper.SwitchTenantAsync(session.Client, otherTenant.Id);
        var switched = await SessionOfAsync(switchedToken);
        switched!.TenantId.Should().Be(otherTenant.Id);
        switched.TenantIdentifier.Should().Be(otherTenant.IdentifierNormalized);
    }

    /// <summary>
    /// Changing a tenant's identifier moves it to another host, so every session in it ends: the old access
    /// token answers 401 and its refresh is refused. A session in another tenant is untouched.
    /// </summary>
    [Fact]
    public async Task Changing_The_Identifier_Ends_The_Tenants_Sessions()
    {
        var tenant = await CreateTenantAsync();
        var otherTenant = await CreateTenantAsync();
        var member = await CreateTenantUserAsync(tenant.Id);
        var otherMember = await CreateTenantUserAsync(otherTenant.Id);
        var session = await SessionForAsync(member.Username, tenant.Id);
        var otherSession = await SessionForAsync(otherMember.Username, otherTenant.Id);

        await SetPlatformAdminAuthTokenAsync();
        var (response, updated) = await Client.PUTAsync<TenantUpdateEndpoint, TenantUpdateRequest, TenantUpdateResponse>(new()
        {
            Id = tenant.Id,
            Name = tenant.Name,
            Identifier = NewTenantIdentifier(),
            EditionId = tenant.EditionId
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        updated.IdentifierNormalized.Should().NotBe(tenant.IdentifierNormalized);

        await AssertEndedAsync(session);
        await AssertAliveAsync(otherSession);
    }

    /// <summary>
    /// An update that leaves the identifier as it was - here a new display name - ends no session.
    /// </summary>
    [Fact]
    public async Task An_Unchanged_Identifier_Ends_No_Session()
    {
        var tenant = await CreateTenantAsync();
        var member = await CreateTenantUserAsync(tenant.Id);
        var session = await SessionForAsync(member.Username, tenant.Id);

        await SetPlatformAdminAuthTokenAsync();
        var (response, _) = await Client.PUTAsync<TenantUpdateEndpoint, TenantUpdateRequest, TenantUpdateResponse>(new()
        {
            Id = tenant.Id,
            Name = $"Renamed {Guid.NewGuid():N}",
            Identifier = tenant.Identifier,
            EditionId = tenant.EditionId
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await AssertAliveAsync(session);
    }

    /// <summary>
    /// A client of its own whose every request carries the gate header and is made to this host.
    /// </summary>
    private HttpClient HostClient(string host)
    {
        var client = App.CreateClient(new ClientOptions { HandleCookies = false });
        client.DefaultRequestHeaders.Host = host;
        client.DefaultRequestHeaders.Add(TenantHostingOverride.GateHeader, "on");
        return client;
    }

    /// <summary>Sets a hub upgrade's host, with the gate header that turns resolution on for it.</summary>
    private static Action<Microsoft.AspNetCore.Http.HttpRequest> OnHost(string host)
        => request =>
        {
            request.Host = new Microsoft.AspNetCore.Http.HostString(host);
            request.Headers[TenantHostingOverride.GateHeader] = "on";
        };

    /// <summary>Signs in on a client without asserting the outcome.</summary>
    private static async Task<(HttpResponseMessage Response, TokenResponse Result)> SignInAsync(HttpClient client, string username, string? tenantIdentifier = null)
    {
        var (response, result) = await client.POSTAsync<TokenEndpoint, TokenRequest, TokenResponse>(new()
        {
            Username = username,
            Password = TestUsers.DefaultPassword,
            TenantIdentifier = tenantIdentifier
        });
        return (response, result);
    }

    /// <summary>Asserts a sign-in is refused with one error code and no session.</summary>
    private static async Task AssertSignInRefusedAsync(HttpClient client, string username, ErrorCode code, string? tenantIdentifier = null)
    {
        var (response, refusal) = await client.POSTAsync<TokenEndpoint, TokenRequest, ProblemDetails>(new()
        {
            Username = username,
            Password = TestUsers.DefaultPassword,
            TenantIdentifier = tenantIdentifier
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, $"sign-in refuses with {code.Value} as it refuses its other tenant errors");
        refusal.Errors.Select(error => error.Code).Should().Equal(code.Value);
    }

    /// <summary>Asserts an access token answers 403 <c>tenantHostMismatch</c> on a host, with a localized reason.</summary>
    private async Task AssertHostMismatchAsync(string host, string accessToken)
    {
        using var client = HostClient(host);
        TestsHelper.SetAuthToken(client, accessToken);

        var (response, refusal) = await client.GETAsync<GetInfoEndpoint, ProblemDetails>();

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden, $"the session does not belong on {host}");
        refusal.Errors.Single().Code.Should().Be(ErrorCodes.TenantHostMismatch.Value);
        refusal.Errors.Single().Reason.Should().NotBeNullOrWhiteSpace();
    }
}
