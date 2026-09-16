namespace Backend.Features.Identity.Endpoints.Account;

using System.Security.Claims;
using Backend.Data.Entities;
using Backend.Features.Identity.Core;
using Backend.Features.Identity.Core.Entities;

/// <summary>
/// Anonymous POST endpoint that authenticates a user by username/email and password and
/// issues a JWT access/refresh token pair along with a refresh-token cookie.
/// </summary>
/// <remarks>
/// The credentials name no tenant: one account is one identity across the whole platform, so the
/// person authenticates once whatever number of tenants they belong to. Which tenant the session
/// starts in is decided here, after authentication, from the memberships the account holds at that
/// moment - exactly one active membership starts the session inside that tenant with nothing for the
/// user to choose, while none and several alike start a session that acts in no tenant, leaving every
/// tenant-scoped operation refused until a tenant is selected. The endpoint is therefore exempt from
/// the active-tenant requirement itself: it is one of the places a tenant is established.
/// </remarks>
[AllowNoTenant]
sealed class TokenEndpoint(IUserService userService, AppDbContext dbContext, IOptions<SigninSetting> signinSetting, IOptions<AuthSetting> authSetting) : Endpoint<TokenRequest, TokenResponse>
{
    /// <summary>
    /// Authentication type of the principal the session being established is evaluated as. It never
    /// authenticates a request; it only names the identity built to ask what this session is entitled to.
    /// </summary>
    private const string SigninAuthenticationType = "Signin";

    public override void Configure()
    {
        Post("token");
        Group<AccountGroup>();
        AllowAnonymous();
    }

    public override async Task HandleAsync(TokenRequest req, CancellationToken c)
    {
        var user = await (!req.IsEmail ? userService.GetByUsernameAsync(req.Username) : userService.GetByEmailAsync(req.Email));
        var errorMessage = !req.IsEmail ? "Username or password is invalid" : "Email or password is invalid";
        var errorCode = !req.IsEmail ? ErrorCodes.InvalidUsernamePassword : ErrorCodes.InvalidEmailPassword;
        if (user == null)
            ThrowError(errorMessage, errorCode);

        var result = await userService.ValidatePasswordAsync(user, req.Password);
        if (!result)
            ThrowError(errorMessage, errorCode);

        // A globally deactivated account is refused here, before any membership is read, so no session is
        // established for it in any tenant and the memberships it holds are neither read for authorization
        // nor written to: they wait untouched for the account to be reactivated.
        if (!user.IsActive)
            ThrowError("User is not active", ErrorCodes.UserNotActive);
        if (signinSetting.Value?.IsEmailVerificationRequired == true && !user.IsEmailVerified)
            ThrowError("Email is not verified", ErrorCodes.EmailNotVerified);

        var tenantId = await ResolveSingleActiveTenantAsync(user.Id, c);

        // Recorded before the token pair is asked for, because the refresh-token row written for this
        // session is all a later refresh has to go on: recording the tenant here is what makes a refresh
        // re-establish the very tenant the session started in rather than none.
        TokenService.RecordSessionTenant(HttpContext, tenantId);

        // The grants the session starts with are read exactly the way every later request re-reads them -
        // for the tenant being acted in and for no other - so the session begins carrying what it would be
        // re-evaluated as on its next request rather than a set assembled only at sign-in.
        var session = await SessionValidator.EvaluateAsync(SigninPrincipal(user, tenantId), dbContext, c);

        var claims = Helper.CreateClaims(user, [.. session.Roles], [.. session.Permissions], tenantId);

        // for cookie authentication
        await CookieAuth.SignInAsync(u =>
        {
            u.Claims.AddRange(claims);
        });

        // for jwt authentication
        Response = await CreateTokenWith<TokenService>(user.Id.ToString(), u =>
        {
            u.Claims.AddRange(claims);
        });

        // Store refresh token in cookie
        if (Response.RefreshToken != null)
        {
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = HttpContext.Request.IsHttps,
                SameSite = SameSiteMode.Strict,
                Expires = DateTime.UtcNow.AddHours(authSetting.Value.RefreshTokenValidity)
            };
            HttpContext.Response.Cookies.Append("refreshToken", $"{Response.UserId}:{Response.RefreshToken}", cookieOptions);
        }

        await userService.UpdateLastSigninAsync(user.Id);
    }

    /// <summary>
    /// The tenant the session starts in: the single tenant the account holds an active membership of, or
    /// <see langword="null"/> when it holds none or holds more than one. A membership counts only while
    /// its row lives and its tenant exists and is not suspended, so an account whose only membership is
    /// of a suspended tenant starts a session with no tenant active rather than one inside a tenant in
    /// which nothing may be done.
    /// </summary>
    /// <param name="userId">The account that has just authenticated.</param>
    /// <param name="cancellationToken">Token used to cancel the read.</param>
    /// <returns>The tenant to start the session in, or <see langword="null"/> when there is not exactly one.</returns>
    /// <remarks>
    /// Two rows answer the whole question - one tenant, or more than one - so no more are read. The read
    /// relaxes tenant restriction by name because it runs before any tenant is established, that being
    /// the very thing it decides; the soft-delete filter stays in force throughout, so a removed
    /// membership places nobody and a deleted tenant is not a tenant to start in.
    /// </remarks>
    private async Task<Guid?> ResolveSingleActiveTenantAsync(Guid userId, CancellationToken cancellationToken)
    {
        var tenantIds = await dbContext.TenantMemberships
            .AsNoTracking()
            .AcrossAllTenants()
            .Where(membership => membership.UserId == userId
                                 && dbContext.Tenants.Any(tenant => tenant.Id == membership.TenantId && tenant.Status == TenantStatus.Active))
            .Select(membership => membership.TenantId)
            .Distinct()
            .Take(2)
            .ToListAsync(cancellationToken);

        return tenantIds.Count == 1 ? tenantIds[0] : null;
    }

    /// <summary>
    /// Builds the principal the session being established is evaluated as, holding the account's identity
    /// and the tenant it acts in and nothing else, so that the roles and permissions it starts with are
    /// read from current data for that tenant rather than assembled separately at sign-in.
    /// </summary>
    /// <param name="user">The account that has just authenticated.</param>
    /// <param name="tenantId">The tenant the session acts in, or <see langword="null"/> for none.</param>
    /// <returns>A principal carrying identity and tenant claims only.</returns>
    private static ClaimsPrincipal SigninPrincipal(User user, Guid? tenantId)
        => new(new ClaimsIdentity(Helper.CreateClaims(user, [], [], tenantId), SigninAuthenticationType));
}

/// <summary>
/// Request payload for the sign-in/token endpoint, allowing sign-in by either username
/// or email (selected via <see cref="IsEmail"/>) together with the user's password.
/// </summary>
/// <remarks>
/// No tenant is named anywhere in it: the tenant a session acts in is resolved from the account's
/// memberships once the credentials are accepted, never supplied alongside them.
/// </remarks>
sealed class TokenRequest
{
    public bool IsEmail { get; set; }
    public string Username { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string Password { get; set; } = null!;
}

/// <summary>
/// FluentValidation rules for <see cref="TokenRequest"/> that conditionally validate the
/// username or email field based on the <see cref="TokenRequest.IsEmail"/> flag, in
/// addition to enforcing password length constraints.
/// </summary>
sealed class TokenRequestValidator : Validator<TokenRequest>
{
    public TokenRequestValidator()
    {
        When(x => !x.IsEmail, () =>
        {
            RuleFor(x => x.Username).NotEmpty().MinimumLength(3).MaximumLength(50);
        });
        When(x => x.IsEmail, () =>
        {
            RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(100);
        });
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(50);
    }
}
