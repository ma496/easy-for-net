using Backend.Features.Identity.Core.Entities;
using Backend.Features.Identity.Endpoints.Account;
using Backend.Features.Tenancy.Core.Entities;
using Backend.Features.Tenancy.Core;
using Backend.Tests.Fakes;
using Backend.Tests.Features.Tenancy;
using TokenRequest = Backend.Features.Identity.Endpoints.Account.TokenRequest;

namespace Backend.Tests.Features.Identity.Endpoints.Account;

/// <summary>
/// Tests for carrying a signed-in account onto a tenant's own host: <c>POST /account/sign-in-ticket</c>
/// issues a single-use ticket on the standing a sign-in there would need, and
/// <c>POST /account/sign-in-ticket/redeem</c> exchanges it on that tenant's host, once, for a session.
/// </summary>
/// <remarks>
/// Requests turn host resolution on for themselves with <see cref="TenantHostingOverride.GateHeader"/>;
/// every tenant and account is created here.
/// </remarks>
public class SignInTicketTests(App app) : TenancyTestsBase(app)
{
    /// <summary>A platform member issues a ticket on the platform host and redeems it on the tenant's host, entering the tenant.</summary>
    [Fact]
    public async Task Ticket_Signs_The_Member_In_On_The_Tenant_Host()
    {
        var (tenant, member) = await PlatformMemberAsync();
        using var platform = await SignedInHostClientAsync(TenantHostingOverride.PlatformHost, member.Username);

        var (issued, ticket) = await platform.POSTAsync<SignInTicketEndpoint, SignInTicketRequest, SignInTicketResponse>(new() { TenantId = tenant.Id });

        issued.StatusCode.Should().Be(HttpStatusCode.OK);
        ticket.TenantUrl.Should().Be(TenantHostingOverride.TenantOrigin(tenant.Identifier));
        ticket.Ticket.Should().NotBeNullOrWhiteSpace();

        using var tenantClient = HostClient(TenantHostingOverride.TenantHost(tenant.Identifier));
        var (redeemed, signedIn) = await tenantClient.POSTAsync<SignInTicketRedeemEndpoint, SignInTicketRedeemRequest, TokenResponse>(new() { Ticket = ticket.Ticket });

        redeemed.StatusCode.Should().Be(HttpStatusCode.OK);
        var session = await SessionOfAsync(signedIn.AccessToken);
        session!.UserId.Should().Be(member.Id);
        session.TenantId.Should().Be(tenant.Id);
        session.TenantIdentifier.Should().Be(tenant.IdentifierNormalized);

        TestsHelper.SetAuthToken(tenantClient, signedIn.AccessToken);
        var (info, _) = await tenantClient.GETAsync<GetInfoEndpoint, UserGetInfoResponse>();
        info.StatusCode.Should().Be(HttpStatusCode.OK, "the redeemed session belongs on the tenant's host");
    }

    /// <summary>A ticket is spent by its first redemption.</summary>
    [Fact]
    public async Task Ticket_Is_Single_Use()
    {
        var (tenant, member) = await PlatformMemberAsync();
        var ticket = await IssueAsync(tenant, member);
        using var tenantClient = HostClient(TenantHostingOverride.TenantHost(tenant.Identifier));

        var (first, _) = await tenantClient.POSTAsync<SignInTicketRedeemEndpoint, SignInTicketRedeemRequest, TokenResponse>(new() { Ticket = ticket });
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        await AssertRedeemRefusedAsync(tenantClient, ticket, ErrorCodes.SignInTicketInvalid);
    }

    /// <summary>A ticket redeems only on its own tenant's host, and a refused redemption still spends it.</summary>
    [Fact]
    public async Task Ticket_Redeems_Only_On_Its_Tenant_Host()
    {
        var (tenant, member) = await PlatformMemberAsync();
        var other = await CreateTenantAsync();

        var ticket = await IssueAsync(tenant, member);
        using var otherClient = HostClient(TenantHostingOverride.TenantHost(other.Identifier));
        await AssertRedeemRefusedAsync(otherClient, ticket, ErrorCodes.TenantHostMismatch);

        using var ownClient = HostClient(TenantHostingOverride.TenantHost(tenant.Identifier));
        await AssertRedeemRefusedAsync(ownClient, ticket, ErrorCodes.SignInTicketInvalid);

        var platformTicket = await IssueAsync(tenant, member);
        using var platformClient = HostClient(TenantHostingOverride.PlatformHost);
        await AssertRedeemRefusedAsync(platformClient, platformTicket, ErrorCodes.TenantHostMismatch);
    }

    /// <summary>A credential change, a lost membership or a suspension between issue and redemption voids the ticket.</summary>
    [Fact]
    public async Task Ticket_Rechecks_The_Standing_It_Was_Issued_On()
    {
        var (tenant, member) = await PlatformMemberAsync();
        using var tenantClient = HostClient(TenantHostingOverride.TenantHost(tenant.Identifier));
        var ct = TestContext.Current.CancellationToken;

        var restamped = await IssueAsync(tenant, member);
        await DbContext.Users.Where(user => user.Id == member.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(user => user.SecurityStamp, Guid.NewGuid()), ct);
        await AssertRedeemRefusedAsync(tenantClient, restamped, ErrorCodes.SignInTicketInvalid);

        var suspended = await IssueAsync(tenant, member);
        await DbContext.Tenants.Where(row => row.Id == tenant.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.Status, TenantStatus.Suspended), ct);
        await AssertRedeemRefusedAsync(tenantClient, suspended, ErrorCodes.TenantSuspended);
        await DbContext.Tenants.Where(row => row.Id == tenant.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.Status, TenantStatus.Active), ct);

        var removed = await IssueAsync(tenant, member);
        await DbContext.TenantMemberships.AcrossAllTenants().Where(row => row.TenantId == tenant.Id && row.UserId == member.Id)
            .ExecuteDeleteAsync(ct);
        await AssertRedeemRefusedAsync(tenantClient, removed, ErrorCodes.SignInTicketInvalid);
    }

    /// <summary>
    /// A session that outlived a credential change cannot mint a session under the new stamp: its ticket
    /// carries the stamp the session was created under, which the redemption no longer accepts.
    /// </summary>
    [Fact]
    public async Task Ticket_From_A_Session_Older_Than_The_Credentials_Is_Refused()
    {
        var (tenant, member) = await PlatformMemberAsync();
        using var platform = await SignedInHostClientAsync(TenantHostingOverride.PlatformHost, member.Username);
        await DbContext.Users.Where(user => user.Id == member.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(user => user.SecurityStamp, Guid.NewGuid()), TestContext.Current.CancellationToken);

        var (issued, ticket) = await platform.POSTAsync<SignInTicketEndpoint, SignInTicketRequest, SignInTicketResponse>(new() { TenantId = tenant.Id });
        issued.StatusCode.Should().Be(HttpStatusCode.OK, "the stale session's record still stands in the store");

        using var tenantClient = HostClient(TenantHostingOverride.TenantHost(tenant.Identifier));
        await AssertRedeemRefusedAsync(tenantClient, ticket.Ticket, ErrorCodes.SignInTicketInvalid);
    }

    /// <summary>An ordinary account signed in on one of its tenants' hosts carries itself onto another tenant it belongs to.</summary>
    [Fact]
    public async Task Ordinary_Account_Moves_Between_Its_Tenants()
    {
        var tenant = await CreateTenantAsync();
        var other = await CreateTenantAsync();
        var member = await CreateTenantUserAsync(tenant.Id);
        await MembershipService.AddAsync(other.Id, member.Id, [], TestContext.Current.CancellationToken);
        using var client = await SignedInHostClientAsync(TenantHostingOverride.TenantHost(tenant.Identifier), member.Username);

        var (issued, ticket) = await client.POSTAsync<SignInTicketEndpoint, SignInTicketRequest, SignInTicketResponse>(new() { TenantId = other.Id });
        issued.StatusCode.Should().Be(HttpStatusCode.OK);
        ticket.TenantUrl.Should().Be(TenantHostingOverride.TenantOrigin(other.Identifier));

        using var otherClient = HostClient(TenantHostingOverride.TenantHost(other.Identifier));
        var (redeemed, signedIn) = await otherClient.POSTAsync<SignInTicketRedeemEndpoint, SignInTicketRedeemRequest, TokenResponse>(new() { Ticket = ticket.Ticket });

        redeemed.StatusCode.Should().Be(HttpStatusCode.OK);
        var session = await SessionOfAsync(signedIn.AccessToken);
        session!.UserId.Should().Be(member.Id);
        session.TenantId.Should().Be(other.Id);
    }

    /// <summary>
    /// Another site cannot drive the redemption from a visitor's browser: a request from another origin is
    /// refused before the ticket is spent, and a form post - the one cross-site request whose cookies a
    /// browser keeps - is refused even with the ticket in the query string.
    /// </summary>
    [Fact]
    public async Task Redemption_From_Another_Origin_Is_Refused()
    {
        var (tenant, member) = await PlatformMemberAsync();
        var ticket = await IssueAsync(tenant, member);
        var ct = TestContext.Current.CancellationToken;
        var redeemPath = $"{App.Services.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>()["RoutePrefix"]}/account/sign-in-ticket/redeem".TrimStart('/');

        using var foreign = HostClient(TenantHostingOverride.TenantHost(tenant.Identifier));
        foreign.DefaultRequestHeaders.Add("Origin", "https://evil.example");
        var (refused, refusal) = await foreign.POSTAsync<SignInTicketRedeemEndpoint, SignInTicketRedeemRequest, ProblemDetails>(new() { Ticket = ticket });
        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        refusal.Errors.Select(error => error.Code).Should().Equal(ErrorCodes.PermissionDenied.Value);

        using var formClient = HostClient(TenantHostingOverride.TenantHost(tenant.Identifier));
        var form = await formClient.PostAsync($"{redeemPath}?ticket={Uri.EscapeDataString(ticket)}", new FormUrlEncodedContent([new("ticket", ticket)]), ct);
        form.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);
        var text = await formClient.PostAsync($"{redeemPath}?ticket={Uri.EscapeDataString(ticket)}", new StringContent($"{{\"ticket\":\"{ticket}\"}}", System.Text.Encoding.UTF8, "text/plain"), ct);
        text.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);

        // None of that spent the ticket: the tenant's own page still redeems it.
        using var own = HostClient(TenantHostingOverride.TenantHost(tenant.Identifier));
        own.DefaultRequestHeaders.Add("Origin", TenantHostingOverride.TenantOrigin(tenant.Identifier));
        var (redeemed, _) = await own.POSTAsync<SignInTicketRedeemEndpoint, SignInTicketRedeemRequest, TokenResponse>(new() { Ticket = ticket });
        redeemed.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>An unknown ticket is refused like a spent one.</summary>
    [Fact]
    public async Task Unknown_Ticket_Is_Refused()
    {
        var tenant = await CreateTenantAsync();
        using var tenantClient = HostClient(TenantHostingOverride.TenantHost(tenant.Identifier));

        await AssertRedeemRefusedAsync(tenantClient, "not-a-ticket", ErrorCodes.SignInTicketInvalid);
    }

    /// <summary>A ticket is issued only for a tenant the caller belongs to, that is active and that has an address.</summary>
    [Fact]
    public async Task Ticket_Is_Issued_On_The_Standing_A_Sign_In_Needs()
    {
        var (tenant, member) = await PlatformMemberAsync();
        var outsideTenant = await CreateTenantAsync();
        var suspendedTenant = await CreateTenantAsync();
        await MembershipService.AddAsync(suspendedTenant.Id, member.Id, [], TestContext.Current.CancellationToken);
        await DbContext.Tenants.Where(row => row.Id == suspendedTenant.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.Status, TenantStatus.Suspended), TestContext.Current.CancellationToken);
        using var platform = await SignedInHostClientAsync(TenantHostingOverride.PlatformHost, member.Username);

        await AssertIssueRefusedAsync(platform, outsideTenant.Id, ErrorCodes.NotTenantMember);
        await AssertIssueRefusedAsync(platform, suspendedTenant.Id, ErrorCodes.TenantSuspended);
        await AssertIssueRefusedAsync(platform, Guid.NewGuid(), ErrorCodes.TenantNotFound);

        // With host resolution off there is no address for the ticket to be carried to.
        using var unresolved = App.CreateClient(new ClientOptions { HandleCookies = false });
        await TestsHelper.SetNewAuthTokenAsync(unresolved, member.Username, TestUsers.DefaultPassword);
        await AssertIssueRefusedAsync(unresolved, tenant.Id, ErrorCodes.TenantAddressNotConfigured);

        using var anonymous = HostClient(TenantHostingOverride.PlatformHost);
        var (response, _) = await anonymous.POSTAsync<SignInTicketEndpoint, SignInTicketRequest, ProblemDetails>(new() { TenantId = tenant.Id });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A platform account that is a member of a new tenant.</summary>
    private async Task<(Tenant Tenant, User Member)> PlatformMemberAsync()
    {
        var tenant = await CreateTenantAsync();
        var member = await CreateTenantUserAsync(tenant.Id);
        await MarkAsPlatformAccountAsync(member.Id);
        return (tenant, member);
    }

    /// <summary>Issues a ticket for the member from the platform host.</summary>
    private async Task<string> IssueAsync(Tenant tenant, User member)
    {
        using var platform = await SignedInHostClientAsync(TenantHostingOverride.PlatformHost, member.Username);
        var (response, ticket) = await platform.POSTAsync<SignInTicketEndpoint, SignInTicketRequest, SignInTicketResponse>(new() { TenantId = tenant.Id });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return ticket.Ticket;
    }

    private async Task<HttpClient> SignedInHostClientAsync(string host, string username)
    {
        var client = HostClient(host);
        var (response, result) = await client.POSTAsync<TokenEndpoint, TokenRequest, TokenResponse>(new()
        {
            Username = username,
            Password = TestUsers.DefaultPassword,
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        TestsHelper.SetAuthToken(client, result.AccessToken);
        return client;
    }

    /// <summary>A client of its own whose every request carries the gate header and is made to this host.</summary>
    private HttpClient HostClient(string host)
    {
        var client = App.CreateClient(new ClientOptions { HandleCookies = false });
        client.DefaultRequestHeaders.Host = host;
        client.DefaultRequestHeaders.Add(TenantHostingOverride.GateHeader, "on");
        return client;
    }

    private static async Task AssertRedeemRefusedAsync(HttpClient client, string ticket, ErrorCode code)
    {
        var (response, refusal) = await client.POSTAsync<SignInTicketRedeemEndpoint, SignInTicketRedeemRequest, ProblemDetails>(new() { Ticket = ticket });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        refusal.Errors.Select(error => error.Code).Should().Equal(code.Value);
    }

    private static async Task AssertIssueRefusedAsync(HttpClient client, Guid tenantId, ErrorCode code)
    {
        var (response, refusal) = await client.POSTAsync<SignInTicketEndpoint, SignInTicketRequest, ProblemDetails>(new() { TenantId = tenantId });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        refusal.Errors.Select(error => error.Code).Should().Equal(code.Value);
    }
}
