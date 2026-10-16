using System.Text.Json.Nodes;
using Backend.External.Email;
using Backend.Features.Identity.Core;
using Backend.Features.Identity.Endpoints.Account;
using Backend.Features.Tenancy.Core;
using Backend.Features.Tenancy.Endpoints.Tenants;
using Backend.Settings;
using Backend.Tests.Fakes;
using Hangfire;
using Microsoft.Extensions.Options;
using TokenRequest = Backend.Features.Identity.Endpoints.Account.TokenRequest;

namespace Backend.Tests.Features.Tenancy.Core.Hosting;

/// <summary>
/// Tests for what the host tells a visitor and where the API's links point once hosts name tenants:
/// <c>GET /tenants/host</c>, <c>GET /tenants/tls-allowed</c>, the origins <c>GET /account/get-info</c>
/// reports, sign-up refused on a tenant host, and the links the account emails carry.
/// </summary>
/// <remarks>
/// Resolution is turned on per request by <see cref="TenantHostingOverride.GateHeader"/>, so nothing here
/// reaches another class; a request without it runs as the shared host does, with resolution off. The
/// account emails that need <c>Signin.IsEmailVerificationRequired</c> see it on through a request-keyed
/// <see cref="PlatformSettingOverlays"/> overlay rather than a platform row, which would refuse sign-ins
/// all over the suite. Every tenant and account is created here.
/// </remarks>
[Collection("TenantHosting")]
public class TenantHostLinksTests(App app) : TenancyTestsBase(app)
{
    private const string UnrelatedHost = "api.unrelated.test";

    private static readonly JsonObject _verificationRequired = new() { ["isEmailVerificationRequired"] = true };

    private string DefaultDomain => Service<IOptions<WebSetting>>().Value.DefaultDomain;

    private RecordingEmailTransport Transport => App.Services.GetRequiredService<RecordingEmailTransport>();

    /// <summary>The platform host, and a host naming nothing, both answer <c>platform</c> with the platform origin.</summary>
    [Fact]
    public async Task Host_Is_Platform_On_The_Platform_Host_And_Where_Hosts_Name_Nothing()
    {
        using var platform = HostClient(TenantHostingOverride.PlatformHost);
        var (platformResponse, onPlatform) = await platform.GETAsync<TenantHostGetEndpoint, TenantHostGetResponse>();

        platformResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        onPlatform.Kind.Should().Be("platform");
        onPlatform.PlatformUrl.Should().Be(TenantHostingOverride.PlatformOrigin);
        onPlatform.Name.Should().BeNull();
        onPlatform.Identifier.Should().BeNull();
        onPlatform.Status.Should().BeNull();
        onPlatform.TenantUrlTemplate.Should().Be($"http://{{tenant}}.{TenantHostingOverride.Suffix}");

        using var unrelated = HostClient(UnrelatedHost);
        var (unrelatedResponse, onUnrelated) = await unrelated.GETAsync<TenantHostGetEndpoint, TenantHostGetResponse>();
        unrelatedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        onUnrelated.Kind.Should().Be("platform");
        onUnrelated.TenantUrlTemplate.Should().Be($"http://{{tenant}}.{TenantHostingOverride.Suffix}",
            "a host naming nothing still answers while resolution is on");

        // Resolution off altogether: the shared host's own configuration.
        using var off = App.CreateClient(new ClientOptions { HandleCookies = false });
        var (offResponse, withResolutionOff) = await off.GETAsync<TenantHostGetEndpoint, TenantHostGetResponse>();
        offResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        withResolutionOff.Kind.Should().Be("platform");
        withResolutionOff.PlatformUrl.Should().Be(DefaultDomain);
        withResolutionOff.TenantUrlTemplate.Should().BeNull("there is no tenant address to preview while resolution is off");
    }

    /// <summary>A tenant host answers its tenant - a suspended one with its status - and an unknown or deleted one 404.</summary>
    [Fact]
    public async Task Host_Is_The_Tenant_On_Its_Host_And_404_For_An_Unknown_Or_Deleted_One()
    {
        var tenant = await CreateTenantAsync();
        using var client = HostClient(TenantHostingOverride.TenantHost(tenant.Identifier));

        var (response, onTenant) = await client.GETAsync<TenantHostGetEndpoint, TenantHostGetResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        onTenant.Kind.Should().Be("tenant");
        onTenant.Name.Should().Be(tenant.Name);
        onTenant.Identifier.Should().Be(tenant.Identifier);
        onTenant.Status.Should().Be(TenantStatus.Active);
        onTenant.PlatformUrl.Should().Be(TenantHostingOverride.PlatformOrigin);
        onTenant.TenantUrlTemplate.Should().Be($"http://{{tenant}}.{TenantHostingOverride.Suffix}");

        var suspended = await CreateTenantAsync(TenantStatus.Suspended);
        using var suspendedClient = HostClient(TenantHostingOverride.TenantHost(suspended.Identifier));
        var (suspendedResponse, onSuspended) = await suspendedClient.GETAsync<TenantHostGetEndpoint, TenantHostGetResponse>();
        suspendedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        onSuspended.Status.Should().Be(TenantStatus.Suspended, "a suspended tenant's host still says whose it is, and that it is suspended");

        await AssertHostNotFoundAsync(TenantHostingOverride.TenantHost($"t-{Guid.NewGuid():N}"));

        var deleted = await CreateTenantAsync();
        await SoftDeleteAsync(deleted.Id);
        await AssertHostNotFoundAsync(TenantHostingOverride.TenantHost(deleted.Identifier));
    }

    /// <summary>
    /// A certificate is allowed for the platform host and an existing tenant's host only - not a deleted
    /// tenant's, an unknown label, an unrelated or malformed domain, nor anything while resolution is off.
    /// </summary>
    [Fact]
    public async Task Tls_Is_Allowed_For_The_Platform_And_Existing_Tenants_Only()
    {
        var tenant = await CreateTenantAsync();
        var suspended = await CreateTenantAsync(TenantStatus.Suspended);
        var deleted = await CreateTenantAsync();
        await SoftDeleteAsync(deleted.Id);
        using var client = HostClient(UnrelatedHost);

        (await TlsAllowedAsync(client, TenantHostingOverride.PlatformHost)).Should().Be(HttpStatusCode.OK);
        (await TlsAllowedAsync(client, TenantHostingOverride.TenantHost(tenant.Identifier))).Should().Be(HttpStatusCode.OK);
        (await TlsAllowedAsync(client, TenantHostingOverride.TenantHost(tenant.Identifier).ToUpperInvariant())).Should().Be(HttpStatusCode.OK);
        (await TlsAllowedAsync(client, TenantHostingOverride.TenantHost(suspended.Identifier))).Should().Be(HttpStatusCode.OK);

        (await TlsAllowedAsync(client, TenantHostingOverride.TenantHost(deleted.Identifier))).Should().Be(HttpStatusCode.NotFound);
        (await TlsAllowedAsync(client, TenantHostingOverride.TenantHost($"t-{Guid.NewGuid():N}"))).Should().Be(HttpStatusCode.NotFound);
        (await TlsAllowedAsync(client, $"{tenant.Identifier}.unrelated.test")).Should().Be(HttpStatusCode.NotFound);
        (await TlsAllowedAsync(client, $"a.{TenantHostingOverride.TenantHost(tenant.Identifier)}")).Should().Be(HttpStatusCode.NotFound);
        (await TlsAllowedAsync(client, $"http://{TenantHostingOverride.TenantHost(tenant.Identifier)}/")).Should().Be(HttpStatusCode.NotFound);
        (await TlsAllowedAsync(client, null)).Should().Be(HttpStatusCode.NotFound);

        using var off = App.CreateClient(new ClientOptions { HandleCookies = false });
        (await TlsAllowedAsync(off, TenantHostingOverride.PlatformHost)).Should().Be(HttpStatusCode.NotFound, "with resolution off no domain is ours");
        (await TlsAllowedAsync(off, TenantHostingOverride.TenantHost(tenant.Identifier))).Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// <c>get-info</c> carries each tenant's origin and the platform origin; with resolution off the tenant
    /// origin is null and the platform origin is <c>Web:DefaultDomain</c>.
    /// </summary>
    [Fact]
    public async Task Get_Info_Carries_Tenant_And_Platform_Origins()
    {
        var tenant = await CreateTenantAsync();
        var otherTenant = await CreateTenantAsync();
        var member = await CreateDualTenantMemberAsync(tenant.Id, otherTenant.Id);
        using var client = HostClient(TenantHostingOverride.TenantHost(tenant.Identifier));
        await SignInOnAsync(client, member.Username);

        var (response, info) = await client.GETAsync<GetInfoEndpoint, UserGetInfoResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        info.PlatformUrl.Should().Be(TenantHostingOverride.PlatformOrigin);
        info.ActiveTenant!.Url.Should().Be(TenantHostingOverride.TenantOrigin(tenant.Identifier));
        info.Tenants.Single(row => row.Id == otherTenant.Id).Url.Should().Be(TenantHostingOverride.TenantOrigin(otherTenant.Identifier));

        using var off = App.CreateClient(new ClientOptions { HandleCookies = false });
        await SignInOnAsync(off, member.Username, tenant.Identifier);
        var (offResponse, offInfo) = await off.GETAsync<GetInfoEndpoint, UserGetInfoResponse>();
        offResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        offInfo.PlatformUrl.Should().Be(DefaultDomain);
        offInfo.Tenants.Should().NotBeEmpty().And.OnlyContain(row => row.Url == null, "no tenant has an origin of its own while resolution is off");
        offInfo.ActiveTenant!.Url.Should().BeNull();
    }

    /// <summary>Sign-up is refused on a tenant host with 400 <c>tenantHostMismatch</c>, and creates nothing.</summary>
    [Fact]
    public async Task Sign_Up_Is_Refused_On_A_Tenant_Host()
    {
        var tenant = await CreateTenantAsync();
        using var client = HostClient(TenantHostingOverride.TenantHost(tenant.Identifier));
        var request = NewSignup();

        var (response, refusal) = await client.POSTAsync<SignupEndpoint, SignupRequest, ProblemDetails>(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        refusal.Errors.Select(error => error.Code).Should().Equal(ErrorCodes.TenantHostMismatch.Value);
        (await DbContext.Users.AnyAsync(user => user.UsernameNormalized == request.Username, TestContext.Current.CancellationToken)).Should().BeFalse();
        (await TenantService.IdentifierExistsAsync(request.TenantIdentifier, cancellationToken: TestContext.Current.CancellationToken)).Should().BeFalse();
    }

    /// <summary>
    /// A sign-up's verification link opens the new tenant's origin; with resolution off it opens
    /// <c>Web:DefaultDomain</c>.
    /// </summary>
    [Fact]
    public async Task Sign_Up_Verification_Links_To_The_New_Tenants_Origin()
    {
        using var platform = HostClient(TenantHostingOverride.PlatformHost);
        RequireVerification(platform);
        var onPlatform = NewSignup();

        var platformResponse = await platform.POSTAsync<SignupEndpoint, SignupRequest, SignupResponse>(onPlatform);

        platformResponse.Response.StatusCode.Should().Be(HttpStatusCode.OK);
        platformResponse.Result.IsEmailVerificationRequired.Should().BeTrue();
        (await LinkMailedToAsync(onPlatform.Email)).Should().StartWith($"{TenantHostingOverride.TenantOrigin(onPlatform.TenantIdentifier)}/verify-email?token=");

        using var off = App.CreateClient(new ClientOptions { HandleCookies = false });
        RequireVerification(off);
        var withResolutionOff = NewSignup();
        (await off.POSTAsync<SignupEndpoint, SignupRequest, SignupResponse>(withResolutionOff)).Response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await LinkMailedToAsync(withResolutionOff.Email)).Should().StartWith($"{DefaultDomain}/verify-email?token=");
    }

    /// <summary>
    /// The password-reset link points at the host the request came from: the tenant's origin on its host, the
    /// platform origin on the platform host, and <c>Web:DefaultDomain</c> where hosts name nothing.
    /// </summary>
    [Fact]
    public async Task Forget_Password_Links_To_The_Requests_Host()
    {
        var tenant = await CreateTenantAsync();

        var onTenant = await CreateTenantUserAsync(tenant.Id);
        using (var client = HostClient(TenantHostingOverride.TenantHost(tenant.Identifier)))
        {
            (await client.POSTAsync<ForgetPasswordEndpoint, ForgetPasswordRequest>(new() { Email = onTenant.Email })).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        (await LinkMailedToAsync(onTenant.Email)).Should().StartWith($"{TenantHostingOverride.TenantOrigin(tenant.Identifier)}/reset-password?token=");

        var onPlatform = await CreateTenantUserAsync(tenant.Id);
        using (var client = HostClient(TenantHostingOverride.PlatformHost))
        {
            (await client.POSTAsync<ForgetPasswordEndpoint, ForgetPasswordRequest>(new() { Email = onPlatform.Email })).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        (await LinkMailedToAsync(onPlatform.Email)).Should().StartWith($"{TenantHostingOverride.PlatformOrigin}/reset-password?token=");

        var withResolutionOff = await CreateTenantUserAsync(tenant.Id);
        using (var client = App.CreateClient(new ClientOptions { HandleCookies = false }))
        {
            (await client.POSTAsync<ForgetPasswordEndpoint, ForgetPasswordRequest>(new() { Email = withResolutionOff.Email })).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        (await LinkMailedToAsync(withResolutionOff.Email)).Should().StartWith($"{DefaultDomain}/reset-password?token=");
    }

    /// <summary>
    /// The link is taken from the host, never from a signed-in caller's session: a member acting in a tenant,
    /// asking on a host that names nothing, gets <c>Web:DefaultDomain</c> rather than its tenant's origin.
    /// </summary>
    [Fact]
    public async Task Forget_Password_Link_Ignores_The_Callers_Session()
    {
        var tenant = await CreateTenantAsync();
        var member = await CreateTenantUserAsync(tenant.Id);
        var account = await CreateTenantUserAsync(tenant.Id);
        using var client = HostClient(UnrelatedHost);
        await SignInOnAsync(client, member.Username);

        (await client.POSTAsync<ForgetPasswordEndpoint, ForgetPasswordRequest>(new() { Email = account.Email })).StatusCode.Should().Be(HttpStatusCode.OK);

        var link = await LinkMailedToAsync(account.Email);
        link.Should().StartWith($"{DefaultDomain}/reset-password?token=");
        link.Should().NotStartWith(TenantHostingOverride.TenantOrigin(tenant.Identifier), "the caller's own tenant decides nothing about the link");
    }

    /// <summary>The re-sent verification link points at the host the request came from, as the reset link does.</summary>
    [Fact]
    public async Task Resend_Verification_Links_To_The_Requests_Host()
    {
        var tenant = await CreateTenantAsync();

        var onTenant = await CreateTenantUserAsync(tenant.Id);
        using (var client = HostClient(TenantHostingOverride.TenantHost(tenant.Identifier)))
        {
            RequireVerification(client);
            (await client.POSTAsync<ResendVerifyEmailEndpoint, ResendVerifyEmailRequest>(new() { EmailOrUsername = onTenant.Username })).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        (await LinkMailedToAsync(onTenant.Email)).Should().StartWith($"{TenantHostingOverride.TenantOrigin(tenant.Identifier)}/verify-email?token=");

        var onPlatform = await CreateTenantUserAsync(tenant.Id);
        using (var client = HostClient(TenantHostingOverride.PlatformHost))
        {
            RequireVerification(client);
            (await client.POSTAsync<ResendVerifyEmailEndpoint, ResendVerifyEmailRequest>(new() { EmailOrUsername = onPlatform.Username })).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        (await LinkMailedToAsync(onPlatform.Email)).Should().StartWith($"{TenantHostingOverride.PlatformOrigin}/verify-email?token=");

        var withResolutionOff = await CreateTenantUserAsync(tenant.Id);
        using (var client = App.CreateClient(new ClientOptions { HandleCookies = false }))
        {
            RequireVerification(client);
            (await client.POSTAsync<ResendVerifyEmailEndpoint, ResendVerifyEmailRequest>(new() { EmailOrUsername = withResolutionOff.Username })).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        (await LinkMailedToAsync(withResolutionOff.Email)).Should().StartWith($"{DefaultDomain}/verify-email?token=");
    }

    /// <summary>A client of its own whose every request carries the gate header and is made to this host.</summary>
    private HttpClient HostClient(string host)
    {
        var client = App.CreateClient(new ClientOptions { HandleCookies = false });
        client.DefaultRequestHeaders.Host = host;
        client.DefaultRequestHeaders.Add(TenantHostingOverride.GateHeader, "on");
        return client;
    }

    /// <summary>Makes this client's requests see email verification required as the platform's value, and nobody else's.</summary>
    private void RequireVerification(HttpClient client)
    {
        var token = Guid.NewGuid().ToString("N");
        Service<PlatformSettingOverlays>().ApplyForRequests(token, IdentitySettingsProvider.SigninSettingName, _verificationRequired);
        client.DefaultRequestHeaders.Add(PlatformSettingOverlays.RequestHeader, token);
    }

    private static SignupRequest NewSignup()
    {
        var username = $"signup-{Guid.NewGuid():N}";
        return new()
        {
            Username = username,
            Email = $"{username}@example.com",
            Password = TestUsers.DefaultPassword,
            ConfirmPassword = TestUsers.DefaultPassword,
            TenantName = $"Tenant {Guid.NewGuid():N}",
            TenantIdentifier = $"t-{Guid.NewGuid():N}"
        };
    }

    /// <summary>Signs a client in, on whatever host it is made to, and keeps the token on it.</summary>
    private static async Task SignInOnAsync(HttpClient client, string username, string? tenantIdentifier = null)
    {
        var (response, signedIn) = await client.POSTAsync<TokenEndpoint, TokenRequest, TokenResponse>(new()
        {
            Username = username,
            Password = TestUsers.DefaultPassword,
            TenantIdentifier = tenantIdentifier
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        TestsHelper.SetAuthToken(client, signedIn.AccessToken);
    }

    private static async Task<HttpStatusCode> TlsAllowedAsync(HttpClient client, string? domain)
        => (await client.GETAsync<TenantTlsAllowedEndpoint, TenantTlsAllowedRequest>(new() { Domain = domain })).StatusCode;

    private async Task AssertHostNotFoundAsync(string host)
    {
        using var client = HostClient(host);
        var (response, refusal) = await client.GETAsync<TenantHostGetEndpoint, ProblemDetails>();

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        refusal.Errors.Select(error => error.Code).Should().Equal(ErrorCodes.TenantNotFound.Value);
    }

    private async Task SoftDeleteAsync(Guid tenantId)
        => await DbContext.Tenants.Where(row => row.Id == tenantId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.IsDeleted, true), TestContext.Current.CancellationToken);

    /// <summary>
    /// Runs the one email job enqueued to <paramref name="recipient"/> the way the worker would, and returns
    /// the link the delivered message carries.
    /// </summary>
    private async Task<string> LinkMailedToAsync(string recipient)
    {
        var monitoring = App.Services.GetRequiredService<JobStorage>().GetMonitoringApi();
        var job = monitoring.EnqueuedJobs("default", 0, int.MaxValue)
            .Select(entry => entry.Value.Job)
            .Where(candidate => candidate?.Method.DeclaringType == typeof(IEmailService) && candidate.Args.Contains(recipient))
            .Should().ContainSingle().Subject;

        using (var scope = App.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IEmailService>();
            await (Task)job.Method.Invoke(service, [.. job.Args])!;
        }

        var body = Transport.SentTo(recipient).Should().ContainSingle().Subject.Message.Body;
        const string marker = "href=\"";
        var start = body.IndexOf(marker, StringComparison.Ordinal);
        start.Should().BeGreaterThanOrEqualTo(0, "the message carries a link");
        start += marker.Length;
        return body[start..body.IndexOf('"', start)];
    }
}
