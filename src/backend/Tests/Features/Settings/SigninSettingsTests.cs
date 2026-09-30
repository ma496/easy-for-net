namespace Backend.Tests.Features.Settings;

using System.Text.Json.Nodes;
using Backend.Features.Identity.Endpoints.Account;
using Backend.Features.Settings.Core;
using Backend.Features.Settings.Endpoints.Settings;
using Backend.Features.Tenancy.Endpoints.Tenants;
using Backend.Features.Tenancy.Core.Entities;
using Backend.Tests.Fakes;

/// <summary>
/// Tests for the <c>Signin</c> setting as sign-in reads it: the value of the tenant being entered, its own
/// override winning over the platform's.
/// </summary>
/// <remarks>
/// The platform's override is simulated through <see cref="PlatformSettingOverlays"/> for tenants this
/// class creates, never written as a platform row - see <see cref="SettingsTestsBase"/>.
/// </remarks>
public class SigninSettingsTests(App app) : SettingsTestsBase(app)
{
    private const string Signin = "Signin";

    /// <summary>The route the refresh-token service registers, addressed directly because a refresh is anonymous.</summary>
    private const string RefreshRoute = "api/account/refresh-token";

    private static readonly JsonObject VerificationRequired = new() { ["isEmailVerificationRequired"] = true };

    private PlatformSettingOverlays Overlays => Service<PlatformSettingOverlays>();

    [Fact]
    public async Task Platform_Requirement_Refuses_An_Unverified_Member_Of_A_Tenant_That_Did_Not_Override_It()
    {
        var tenant = await CreateTenantAsync();
        var member = await CreateTenantUserAsync(tenant.Id);
        Overlays.Apply(tenant.Id, Signin, VerificationRequired);

        var (response, refusal) = await SignInAsync<ProblemDetails>(member.Username, tenant);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        refusal.Errors.Select(e => e.Code).Should().Contain(ErrorCodes.EmailNotVerified.Value);
    }

    [Fact]
    public async Task Platform_Requirement_Admits_A_Verified_Member()
    {
        var tenant = await CreateTenantAsync();
        var member = await CreateTenantUserAsync(tenant.Id);
        await MarkEmailVerifiedAsync(member);
        Overlays.Apply(tenant.Id, Signin, VerificationRequired);

        var (response, _) = await SignInAsync<TokenResponse>(member.Username, tenant);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Tenant_Override_Turning_It_Off_Admits_An_Unverified_Member()
    {
        var tenant = await CreateTenantAsync();
        var member = await CreateTenantUserAsync(tenant.Id);
        // Signed in before the platform requirement stands: the administrator's account is unverified too.
        var administrator = await TenantClientAsync(tenant.Id);
        Overlays.Apply(tenant.Id, Signin, VerificationRequired);

        var (listResponse, before) = await administrator.GETAsync<SettingListEndpoint, SettingListResponse>();
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK, "a session created before the requirement keeps working");
        PropertyOf(before, Signin, "isEmailVerificationRequired").Value!.GetValue<bool>().Should().BeTrue();
        PropertyOf(before, Signin, "isEmailVerificationRequired").Source.Should().Be("platform");

        var (putResponse, updated) = await administrator.PUTAsync<SettingUpdateEndpoint, SettingUpdateRequest, SettingDto>(
            new() { Name = Signin, Values = new JsonObject { ["isEmailVerificationRequired"] = false } });
        putResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        PropertyOf(updated, "isEmailVerificationRequired").Source.Should().Be("tenant");
        (await StoredValuesAsync(Signin, null)).Should().BeNull("a tenant's write never reaches the platform row");

        var (response, _) = await SignInAsync<TokenResponse>(member.Username, tenant);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Tenant_Override_Is_Its_Own_And_Another_Tenant_Still_Follows_The_Platform()
    {
        var relaxed = await CreateTenantAsync();
        var strict = await CreateTenantAsync();
        var relaxedMember = await CreateTenantUserAsync(relaxed.Id);
        var strictMember = await CreateTenantUserAsync(strict.Id);
        await SetTenantValuesAsync(relaxed.Id, Signin, new JsonObject { ["isEmailVerificationRequired"] = false });
        Overlays.Apply(relaxed.Id, Signin, VerificationRequired);
        Overlays.Apply(strict.Id, Signin, VerificationRequired);

        var (relaxedResponse, _) = await SignInAsync<TokenResponse>(relaxedMember.Username, relaxed);
        var (strictResponse, refusal) = await SignInAsync<ProblemDetails>(strictMember.Username, strict);

        relaxedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        strictResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        refusal.Errors.Select(e => e.Code).Should().Contain(ErrorCodes.EmailNotVerified.Value);
    }

    [Fact]
    public async Task Tenant_Requirement_Refuses_Sign_In_And_Revokes_No_Existing_Session()
    {
        var tenant = await CreateTenantAsync();
        var member = await CreateTenantUserAsync(tenant.Id);
        var administrator = await TenantClientAsync(tenant.Id);

        var (putResponse, _) = await administrator.PUTAsync<SettingUpdateEndpoint, SettingUpdateRequest, SettingDto>(
            new() { Name = Signin, Values = VerificationRequired.DeepClone().AsObject() });
        putResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        (await administrator.GETAsync<SettingListEndpoint, SettingListResponse>()).Response.StatusCode
            .Should().Be(HttpStatusCode.OK, "a settings change revokes no session, even one it would now refuse to create");

        var (response, refusal) = await SignInAsync<ProblemDetails>(member.Username, tenant);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        refusal.Errors.Select(e => e.Code).Should().Contain(ErrorCodes.EmailNotVerified.Value);
    }

    [Fact]
    public async Task Switching_Into_A_Tenant_That_Requires_Verification_Refuses_An_Unverified_Member()
    {
        var relaxed = await CreateTenantAsync();
        var strict = await CreateTenantAsync();
        var member = await CreateDualTenantMemberAsync(relaxed.Id, strict.Id);
        await SetTenantValuesAsync(relaxed.Id, Signin, new JsonObject { ["isEmailVerificationRequired"] = false });
        Overlays.Apply(relaxed.Id, Signin, VerificationRequired);
        Overlays.Apply(strict.Id, Signin, VerificationRequired);
        var client = await ClientForAsync(member.Username, relaxed.Id);

        var (response, refusal) = await client.POSTAsync<TenantSwitchEndpoint, TenantSwitchRequest, ProblemDetails>(
            new() { TenantId = strict.Id });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "entering a tenant is held to its sign-in setting exactly as signing in to it is");
        refusal.Errors.Select(e => e.Code).Should().Contain(ErrorCodes.EmailNotVerified.Value);
    }

    [Fact]
    public async Task Switching_Into_A_Tenant_That_Does_Not_Require_Verification_Admits_An_Unverified_Member()
    {
        var first = await CreateTenantAsync();
        var second = await CreateTenantAsync();
        var member = await CreateDualTenantMemberAsync(first.Id, second.Id);
        await SetTenantValuesAsync(first.Id, Signin, new JsonObject { ["isEmailVerificationRequired"] = false });
        await SetTenantValuesAsync(second.Id, Signin, new JsonObject { ["isEmailVerificationRequired"] = false });
        Overlays.Apply(first.Id, Signin, VerificationRequired);
        Overlays.Apply(second.Id, Signin, VerificationRequired);
        var client = await ClientForAsync(member.Username, first.Id);

        var (response, switched) = await client.POSTAsync<TenantSwitchEndpoint, TenantSwitchRequest, TenantSwitchResponse>(
            new() { TenantId = second.Id });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        switched.TenantId.Should().Be(second.Id);
    }

    [Fact]
    public async Task Tenant_Requirement_Set_After_Sign_In_Refuses_The_Unverified_Members_Next_Refresh()
    {
        var tenant = await CreateTenantAsync();
        var member = await CreateTenantUserAsync(tenant.Id);
        var session = await SessionForAsync(member.Username, tenant.Id);
        var administrator = await TenantClientAsync(tenant.Id);

        var (putResponse, _) = await administrator.PUTAsync<SettingUpdateEndpoint, SettingUpdateRequest, SettingDto>(
            new() { Name = Signin, Values = VerificationRequired.DeepClone().AsObject() });
        putResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var anonymous = App.CreateClient(new ClientOptions { HandleCookies = false });
        var (response, refusal) = await anonymous.POSTAsync<FastEndpoints.Security.TokenRequest, ProblemDetails>(
            RefreshRoute,
            new() { UserId = session.UserId.ToString(), RefreshToken = session.RefreshToken });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "the setting standing at refresh decides, with nothing revoked when it changed");
        refusal.Errors.Select(e => e.Code).Should().Contain(ErrorCodes.EmailNotVerified.Value);
    }

    /// <summary>Signs an account in to a tenant on an anonymous client of its own, answering whatever sign-in answered.</summary>
    private async Task<TestResult<TResponse>> SignInAsync<TResponse>(string username, Tenant tenant)
    {
        var anonymous = App.CreateClient(new ClientOptions { HandleCookies = false });
        return await anonymous.POSTAsync<TokenEndpoint, TokenRequest, TResponse>(new()
        {
            Username = username,
            Password = TestUsers.DefaultPassword,
            TenantIdentifier = tenant.Identifier
        });
    }
}
