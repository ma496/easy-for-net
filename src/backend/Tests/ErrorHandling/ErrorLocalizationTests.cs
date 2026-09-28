namespace Backend.Tests.ErrorHandling;

using System.Net.Http.Headers;
using Backend.Features.Identity.Endpoints.Users;
using Backend.Features.Localization.Core;
using Backend.Tests.Features.Localization;
using Backend.Tests.Seeder;

/// <summary>
/// Tests that a business error's message - one carrying an <see cref="ErrorCodes"/> value with a
/// matching <c>error.server.&lt;code&gt;</c> resource key - is served in the culture the request
/// negotiated via <c>Accept-Language</c>, through the acting scope's own override chain, while a plain
/// FluentValidation rule with no such key is left exactly as FluentValidation wrote it.
/// </summary>
/// <remarks>
/// Derives from <see cref="LocalizationTestsBase"/> (the <c>Localization</c> xunit collection) because
/// every case here reads the same globally-shared platform resource layer that class's own tests write
/// overrides into, whether or not this particular test writes one itself.
/// </remarks>
public class ErrorLocalizationTests(App app) : LocalizationTestsBase(app)
{
    /// <summary>
    /// Builds a valid create-user request, so a test only has to override the field that makes its
    /// case (a duplicate username, an unknown role, ...).
    /// </summary>
    private static Faker<UserCreateRequest> ValidRequestFaker()
        => new Faker<UserCreateRequest>()
            .RuleFor(u => u.Username, f => f.Internet.UserName() + f.UniqueIndex)
            .RuleFor(u => u.Email, f => f.Internet.Email() + f.UniqueIndex)
            .RuleFor(u => u.Password, f => f.Internet.Password())
            // Padded because Bogus draws two-letter names ("Al", "Jo") that fail MinimumLength(3) before
            // the handler can raise the coded error these tests assert on.
            .RuleFor(u => u.FirstName, f => f.Name.FirstName().PadRight(3, 'x'))
            .RuleFor(u => u.LastName, f => f.Name.LastName().PadRight(3, 'x'))
            .RuleFor(u => u.IsActive, f => true)
            .RuleFor(u => u.Roles, _ => [TestRoles.TestRoleId]);

    [Fact]
    public async Task Coded_Error_Is_Served_In_The_Requested_Culture()
    {
        Assert.SkipWhen(!ResourceStore.ShippedCultures.Contains("ar", StringComparer.OrdinalIgnoreCase),
            "this test needs Arabic shipped, and a project generated with -m false ships English only");

        await SetAuthTokenAsync();
        Client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("ar"));

        var request = ValidRequestFaker().Generate();
        request.Username = TestUsers.TenantAdminUsername; // already exists, seeded

        var (response, refusal) = await Client.POSTAsync<UserCreateEndpoint, UserCreateRequest, ProblemDetails>(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = refusal.Errors.Should().ContainSingle(e => e.Code == ErrorCodes.UsernameAlreadyExists.Value).Which;
        error.Reason.Should().Be(ResourceStore.GetResources("ar")!["error.server.usernameAlreadyExists"],
            "the request named Arabic, and the code has a resource key for it");
        refusal.Detail.Should().Be(error.Reason, "a single-error response mirrors its own error in `detail`");
    }

    [Fact]
    public async Task No_Accept_Language_Header_Serves_English()
    {
        await SetAuthTokenAsync();

        var request = ValidRequestFaker().Generate();
        request.Username = TestUsers.TenantAdminUsername;

        var (response, refusal) = await Client.POSTAsync<UserCreateEndpoint, UserCreateRequest, ProblemDetails>(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = refusal.Errors.Should().ContainSingle(e => e.Code == ErrorCodes.UsernameAlreadyExists.Value).Which;
        error.Reason.Should().Be(ResourceStore.EnglishResources["error.server.usernameAlreadyExists"]);
    }

    /// <remarks>
    /// A call site names only <see cref="ErrorCodes.UsernameAlreadyExists"/> - <c>UserCreateEndpoint</c>
    /// calls <c>ThrowError(ErrorCodes.UsernameAlreadyExists)</c> with no message argument at all -
    /// so this is the guard that the response still carries the stable machine-readable code a client
    /// branches on, distinct from the sentence a person reads, rather than the code doing duty as both.
    /// </remarks>
    [Fact]
    public async Task Coded_Response_Carries_The_Code_Separately_From_Its_Message()
    {
        await SetAuthTokenAsync();

        var request = ValidRequestFaker().Generate();
        request.Username = TestUsers.TenantAdminUsername;

        var (response, refusal) = await Client.POSTAsync<UserCreateEndpoint, UserCreateRequest, ProblemDetails>(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = refusal.Errors.Should().ContainSingle().Which;
        error.Code.Should().Be(ErrorCodes.UsernameAlreadyExists.Value);
        error.Name.Should().Be("generalErrors",
            "a request-level error is filed under FastEndpoints' general-errors field, as its own overloads file it");
        error.Reason.Should().NotBe(ErrorCodes.UsernameAlreadyExists.Value,
            "the caller's own message text, never the bare code, belongs in `reason`");
        error.Reason.Should().Be(ResourceStore.EnglishResources["error.server.usernameAlreadyExists"]);
    }

    [Fact]
    public async Task Unknown_Accept_Language_Falls_Back_To_English()
    {
        await SetAuthTokenAsync();
        Client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("xx-XX"));

        var request = ValidRequestFaker().Generate();
        request.Username = TestUsers.TenantAdminUsername;

        var (response, refusal) = await Client.POSTAsync<UserCreateEndpoint, UserCreateRequest, ProblemDetails>(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = refusal.Errors.Should().ContainSingle(e => e.Code == ErrorCodes.UsernameAlreadyExists.Value).Which;
        error.Reason.Should().Be(ResourceStore.EnglishResources["error.server.usernameAlreadyExists"]);
    }

    /// <remarks>
    /// Deliberately requests Arabic - the adversarial case for this mechanism - rather than comparing
    /// the message across cultures: FluentValidation ships its own per-culture default messages and
    /// picks among them from the same ambient <see cref="System.Globalization.CultureInfo.CurrentUICulture"/>
    /// the request localization middleware sets, entirely on its own account, so a plain rule's message
    /// is not actually culture-invariant once that middleware exists. What this mechanism promises is
    /// narrower and is what is asserted here: it never substitutes an <c>error.server.*</c> value for a
    /// code with no resource key of its own, which <c>NotEmptyValidator</c> - a FluentValidation-assigned
    /// code, never one of <see cref="ErrorCodes"/> - is not shipped with.
    /// </remarks>
    [Fact]
    public async Task Plain_FluentValidation_Error_Has_No_Resource_Key_And_Is_Left_To_FluentValidation()
    {
        await SetAuthTokenAsync();
        Client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("ar"));

        var request = ValidRequestFaker().Generate();
        request.Username = ""; // NotEmptyValidator, with no ErrorCodes constant and no resource key

        var (response, refusal) = await Client.POSTAsync<UserCreateEndpoint, UserCreateRequest, ProblemDetails>(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = refusal.Errors.Should().ContainSingle(e => e.Name == "username").Which;
        ResourceStore.EnglishResources.Should().NotContainKey($"error.server.{error.Code}",
            "a plain FluentValidation rule carries no ErrorCodes constant, so this mechanism has nothing to substitute");
    }

    [Fact]
    public async Task Tenant_Override_Of_A_Business_Error_Is_Served_For_That_Tenant()
    {
        var tenant = await CreateTenantAsync();
        var roleId = await CreateTenantRoleAsync(tenant.Id, Allow.User_Create);
        var member = await CreateTenantUserAsync(tenant.Id, roleId);
        var client = await ClientForAsync(member.Username, tenant.Id);

        await SetTenantTextAsync(tenant.Id, "en", "error.server.usernameAlreadyExists", "This tenant already has that username.");

        var request = ValidRequestFaker().Generate();
        request.Username = TestUsers.TenantAdminUsername;

        var (response, refusal) = await client.POSTAsync<UserCreateEndpoint, UserCreateRequest, ProblemDetails>(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = refusal.Errors.Should().ContainSingle(e => e.Code == ErrorCodes.UsernameAlreadyExists.Value).Which;
        error.Reason.Should().Be("This tenant already has that username.");
    }

    /// <remarks>
    /// The permission refusal is answered by <c>AuthorizationRefusalResultHandler</c>, inside
    /// <c>UseAuthorization</c> and therefore ahead of every FastEndpoints pre-processor including
    /// <c>TenantContextProcessor</c> - so unlike every other case in this suite, the acting tenant is
    /// never established through the ordinary route at all, and this exercises the fallback that reads
    /// it from the session's own claim instead.
    /// </remarks>
    [Fact]
    public async Task Tenant_Override_Of_PermissionDenied_Is_Served_To_The_Member_It_Refuses()
    {
        var tenant = await CreateTenantAsync();
        var roleWithNoPermissions = await CreateTenantRoleAsync(tenant.Id);
        var member = await CreateTenantUserAsync(tenant.Id, roleWithNoPermissions);
        var client = await ClientForAsync(member.Username, tenant.Id);

        await SetTenantTextAsync(tenant.Id, Culture, "error.server.permissionDenied", "This tenant says no.");

        var (response, refusal) = await client.GETAsync<UserListEndpoint, UserListRequest, ProblemDetails>(new());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var error = refusal.Errors.Should().ContainSingle(e => e.Code == ErrorCodes.PermissionDenied.Value).Which;
        error.Reason.Should().Be("This tenant says no.");
    }

    [Fact]
    public async Task Tenant_Override_Of_PermissionDenied_Does_Not_Reach_Another_Tenants_Member_Or_An_Anonymous_Caller()
    {
        var tenantWithOverride = await CreateTenantAsync();
        await SetTenantTextAsync(tenantWithOverride.Id, Culture, "error.server.permissionDenied", "This tenant says no.");

        var otherTenant = await CreateTenantAsync();
        var roleWithNoPermissions = await CreateTenantRoleAsync(otherTenant.Id);
        var otherMember = await CreateTenantUserAsync(otherTenant.Id, roleWithNoPermissions);
        var otherClient = await ClientForAsync(otherMember.Username, otherTenant.Id);

        var (otherResponse, otherRefusal) = await otherClient.GETAsync<UserListEndpoint, UserListRequest, ProblemDetails>(new());

        otherResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var otherError = otherRefusal.Errors.Should().ContainSingle(e => e.Code == ErrorCodes.PermissionDenied.Value).Which;
        otherError.Reason.Should().Be(ResourceStore.EnglishResources["error.server.permissionDenied"],
            "the override belongs to a different tenant, and this member's own session names none");

        var anonymousClient = App.CreateClient(new ClientOptions { HandleCookies = false });
        var (anonymousResponse, anonymousRefusal) = await anonymousClient
            .GETAsync<UserListEndpoint, UserListRequest, ProblemDetails>(new());

        anonymousResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "an anonymous caller is challenged for an account rather than told which permission it lacks");
        anonymousRefusal.Errors.Should().ContainSingle(e => e.Code == ErrorCodes.AuthenticationRequired.Value);
    }

    /// <remarks>
    /// <c>ExceptionProcessor</c> answers a production 500 through this exact code rather than the
    /// exception's own message - this proves the key resolves, without needing an endpoint that can be
    /// made to crash on demand.
    /// </remarks>
    [Fact]
    public async Task InternalServerError_Key_Resolves_To_The_Shipped_Message()
    {
        var localizer = Service<IErrorMessageLocalizer>();

        var shipped = await localizer.TryLocalizeErrorAsync(
            "en", ErrorCodes.InternalServerError.Value, string.Empty, cancellationToken: TestContext.Current.CancellationToken);

        shipped.Should().Be(ResourceStore.EnglishResources["error.server.internalServerError"]);
    }

    /// <remarks>
    /// A fresh instance for this test rather than a second call on the one above: the resolution cache
    /// keys on the tenant and culture only, not on the key looked up within them, so a second call on
    /// the same <see cref="IErrorMessageLocalizer"/> would still be answered from its first resolution
    /// - written before <see cref="LocalizationTestsBase.SetPlatformTextAsync"/> planted this override,
    /// bypassing the cache invalidation only <c>LocalizationService</c>'s own write path applies.
    /// </remarks>
    [Fact]
    public async Task InternalServerError_Key_Honors_A_Platform_Override()
    {
        await SetPlatformTextAsync(Culture, "error.server.internalServerError", "Something went sideways.");

        var localizer = Service<IErrorMessageLocalizer>();
        var overridden = await localizer.TryLocalizeErrorAsync(
            "en", ErrorCodes.InternalServerError.Value, string.Empty, cancellationToken: TestContext.Current.CancellationToken);

        overridden.Should().Be("Something went sideways.");
    }

    [Fact]
    public async Task PropertyName_Placeholder_Interpolates_The_Localized_Field_Label()
    {
        await SetAuthTokenAsync();
        await SetPlatformTextAsync(Culture, "error.server.referencedRecordNotFound", "The ${propertyName} field cannot be found.");

        var request = ValidRequestFaker().Generate();
        request.Roles = [Guid.NewGuid()]; // not a real role, so the reference check fails

        var (response, refusal) = await Client.POSTAsync<UserCreateEndpoint, UserCreateRequest, ProblemDetails>(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = refusal.Errors.Should().ContainSingle(e => e.Code == ErrorCodes.ReferencedRecordNotFound.Value).Which;
        error.Name.Should().Be("roles");
        error.Reason.Should().Be("The roles field cannot be found.",
            "no top-level 'roles' label is shipped, so the raw field name is used as its own placeholder value");
    }
}
