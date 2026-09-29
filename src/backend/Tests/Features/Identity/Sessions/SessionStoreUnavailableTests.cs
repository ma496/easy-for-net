namespace Backend.Tests.Features.Identity.Sessions;

using Backend.Features.Identity.Endpoints.Account;
using Backend.Features.Localization.Core;
using Backend.Features.Localization.Endpoints.Localization;
using Backend.Tests.Fakes;
using Backend.Tests.Features.Tenancy;
using Microsoft.AspNetCore.Authentication.Cookies;

/// <summary>
/// Tests that a session store which cannot be reached fails closed: a 503 <c>sessionStoreUnavailable</c>,
/// in the caller's language, and never a 401 and never an anonymous caller. The store is made
/// unreachable for one session or account a test created (see <see cref="SessionStoreFaults"/>), so no
/// test running beside it notices.
/// </summary>
public class SessionStoreUnavailableTests(App app) : TenancyTestsBase(app)
{
    private const string RefreshRoute = "api/account/refresh-token";

    /// <summary>
    /// Verifies a request whose session cannot be looked up is answered 503 with the coded, localized
    /// error - in English by default and in Spanish when the caller asks for it.
    /// </summary>
    [Theory]
    [InlineData("en")]
    [InlineData("es")]
    public async Task Unreachable_Store_Answers_503_In_The_Callers_Language(string culture)
    {
        await SetAuthTokenAsync();
        Service<SessionStoreFaults>().MakeUnreachable(SessionIdOfClient());
        Client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(culture);

        var (response, problem) = await Client.GETAsync<GetInfoEndpoint, ProblemDetails>();

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        var error = problem.Errors.Single();
        error.Code.Should().Be(ErrorCodes.SessionStoreUnavailable.Value);
        error.Reason.Should().Be(Service<ILocalizationResourceStore>().GetResources(culture)!["error.server.sessionStoreUnavailable"],
            "the message is the shipped one for the caller's culture");
    }

    /// <summary>
    /// Verifies the message really is translated: the Spanish text differs from the English one.
    /// </summary>
    [Fact]
    public void Message_Is_Translated_Not_Copied()
    {
        var store = Service<ILocalizationResourceStore>();
        var english = store.EnglishResources["error.server.sessionStoreUnavailable"];

        store.ShippedCultures.Where(culture => culture != "en")
            .Should().OnlyContain(culture => store.GetResources(culture)!["error.server.sessionStoreUnavailable"] != english);
    }

    /// <summary>
    /// Verifies an endpoint that allows anonymous callers does not treat a caller whose session cannot be
    /// looked up as anonymous: it answers 503 rather than serving.
    /// </summary>
    [Fact]
    public async Task Anonymous_Endpoint_Does_Not_Answer_A_Credential_It_Could_Not_Check()
    {
        await SetAuthTokenAsync();
        Service<SessionStoreFaults>().MakeUnreachable(SessionIdOfClient());

        var (response, problem) = await Client
            .GETAsync<ResourcesGetEndpoint, ResourcesGetRequest, ProblemDetails>(new() { Culture = "en" });

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        problem.Errors.Single().Code.Should().Be(ErrorCodes.SessionStoreUnavailable.Value);
    }

    /// <summary>
    /// Verifies the same on the cookie path.
    /// </summary>
    [Fact]
    public async Task Unreachable_Store_Answers_503_For_A_Cookie_Too()
    {
        var signIn = App.CreateClient(new ClientOptions { HandleCookies = false });
        var (signedIn, tokens) = await signIn.POSTAsync<TokenEndpoint, TokenRequest, TokenResponse>(
            new() { Username = TestUsers.TenantAdminUsername, Password = TestUsers.AdminPassword });
        var cookieName = Service<Microsoft.Extensions.Options.IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme).Cookie.Name!;
        var cookie = signedIn.Headers.GetValues("Set-Cookie").Select(header => header.Split(';')[0])
            .Single(pair => pair.StartsWith(cookieName + "=", StringComparison.Ordinal));
        Service<SessionStoreFaults>().MakeUnreachable(TestsHelper.PayloadOf(tokens.AccessToken)["sid"].GetString()!);

        var client = App.CreateClient(new ClientOptions { HandleCookies = false });
        client.DefaultRequestHeaders.Add("Cookie", cookie);
        var (response, problem) = await client.GETAsync<GetInfoEndpoint, ProblemDetails>();

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        problem.Errors.Single().Code.Should().Be(ErrorCodes.SessionStoreUnavailable.Value);
    }

    /// <summary>
    /// Verifies a store failure while a session is being minted (sign-in) is answered the same way.
    /// </summary>
    [Fact]
    public async Task Sign_In_Answers_503_When_The_Session_Cannot_Be_Stored()
    {
        var tenant = await CreateTenantAsync();
        var account = await CreateTenantUserAsync(tenant.Id);
        Service<SessionStoreFaults>().MakeUnwritable(account.Id);
        var client = App.CreateClient(new ClientOptions { HandleCookies = false });

        var (response, problem) = await client.POSTAsync<TokenEndpoint, TokenRequest, ProblemDetails>(
            new() { Username = account.Username, Password = TestUsers.DefaultPassword, TenantIdentifier = tenant.Identifier });

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        problem.Errors.Single().Code.Should().Be(ErrorCodes.SessionStoreUnavailable.Value);
    }

    /// <summary>
    /// Verifies a store failure while a session is being renewed is answered the same way.
    /// </summary>
    [Fact]
    public async Task Refresh_Answers_503_When_The_Session_Cannot_Be_Stored()
    {
        var account = await CreateTenantUserAsync((await CreateTenantAsync()).Id);
        var session = await SessionForAsync(account.Username);
        Service<SessionStoreFaults>().MakeUnwritable(account.Id);
        var anonymous = App.CreateClient(new ClientOptions { HandleCookies = false });

        var (response, problem) = await anonymous.POSTAsync<FastEndpoints.Security.TokenRequest, ProblemDetails>(
            RefreshRoute, new() { UserId = account.Id.ToString(), RefreshToken = session.RefreshToken });

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        problem.Errors.Single().Code.Should().Be(ErrorCodes.SessionStoreUnavailable.Value);
    }

    private string SessionIdOfClient()
        => TestsHelper.PayloadOf(Client.DefaultRequestHeaders.Authorization!.Parameter!)["sid"].GetString()!;
}
