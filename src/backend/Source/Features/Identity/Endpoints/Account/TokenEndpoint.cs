namespace Backend.Features.Identity.Endpoints.Account;

using Backend.Features.Tenancy.Core;
using Backend.Features.Identity.Core;
using Backend.Features.Identity.Core.Entities;

/// <summary>
/// Anonymous POST endpoint that authenticates a user by username/email and password and
/// issues a JWT access/refresh token pair along with a refresh-token cookie.
/// </summary>
/// <remarks>
/// The credentials name no tenant: one account is one identity across the whole platform, so the
/// person authenticates once whatever number of tenants they belong to. Which tenant the session
/// starts in is decided here, after authentication, and never from the credentials themselves.
/// <para>
/// The request may name a tenant beside the credentials, and that is a convenience rather than a
/// second credential: it saves a person who belongs to several tenants the detour through the chooser
/// by stating up front which one they came to work in. It is authorized here on exactly the standing
/// that <c>POST /tenants/switch</c> would authorize it on a moment later, and a tenant the account
/// cannot act in refuses the sign-in outright rather than quietly starting a session somewhere else:
/// a person who named a tenant is told why they did not get it.
/// </para>
/// <para>
/// With no tenant named, the session starts in the single active membership the account holds, and
/// that is the only way it starts without being told. An ordinary account holding none, or holding
/// several, is asked to name one rather than signed in with no tenant at all: such a session carries
/// no permission whatever, so establishing one would authenticate somebody into a state where
/// nothing they try can succeed. A platform account is the exception - it belongs to no tenant and
/// works platform-wide - so it signs in with none and enters a tenant when it needs one.
/// </para>
/// </remarks>
sealed class TokenEndpoint(IUserService userService, AppDbContext dbContext, IPermissionFeatureFilter permissionFeatureFilter, IOptions<SigninSetting> signinSetting, IOptions<AuthSetting> authSetting) : Endpoint<TokenRequest, TokenResponse>
{
    /// <summary>
    /// The refusal reported for a tenant identifier that names no tenant, or names a deleted one. It
    /// carries no detail, so the answer for a tenant that never existed and the answer for one that is
    /// gone are the same answer.
    /// </summary>
    private const string TenantNotFoundMessage = "Tenant not found";

    /// <summary>
    /// The refusal reported when the account holds no active membership in the tenant it named.
    /// Holding permissions - even every permission - in another tenant is not standing in this one;
    /// only the platform tier, which belongs to no tenant at all, is.
    /// </summary>
    private const string NotTenantMemberMessage = "You are not a member of this tenant";

    /// <summary>
    /// The refusal reported when the tenant named is suspended. A suspended tenant is out of service
    /// rather than gone, so signing in to work inside it is refused while it is.
    /// </summary>
    private const string TenantSuspendedMessage = "The tenant is suspended";

    /// <summary>
    /// The refusal reported when an ordinary account names no tenant and its memberships do not settle
    /// the question by themselves - it holds none, or it holds more than one. Naming a tenant is what
    /// resolves it, so the failure is raised against that field.
    /// </summary>
    private const string TenantRequiredMessage = "Please provide a tenant to sign in.";

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

        var tenantId = string.IsNullOrWhiteSpace(req.TenantIdentifier)
            ? await ResolveUnnamedTenantAsync(user, c)
            : await ResolveNamedTenantAsync(user, req.TenantIdentifier, c);

        // Recorded before the token pair is asked for, because the refresh-token row written for this
        // session is all a later refresh has to go on: recording the tenant here is what makes a refresh
        // re-establish the very tenant the session started in rather than none.
        TokenService.RecordSessionTenant(HttpContext, tenantId);

        // The grants the session starts with are read for the tenant being acted in and for no other,
        // narrowed to the scope that tenant puts the session in. They are what every request made with
        // this token is authorized on, until it is renewed, switched or replaced by a new sign-in.
        var grants = await SessionGrants.ReadAsync(dbContext, permissionFeatureFilter, user.Id, tenantId, user.IsPlatform, c);

        var claims = Helper.CreateClaims(user, grants.Roles, grants.Permissions, tenantId);

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
    /// The tenant the session starts in when the request named none: the single tenant the account
    /// holds an active membership of. A membership counts only while its row lives and its tenant
    /// exists and is not suspended, so an account whose only membership is of a suspended tenant has
    /// no tenant to start in rather than one inside a tenant in which nothing may be done.
    /// </summary>
    /// <param name="user">The account that has just authenticated.</param>
    /// <param name="cancellationToken">Token used to cancel the read.</param>
    /// <returns>The tenant to start the session in, or <see langword="null"/> for a platform account.</returns>
    /// <remarks>
    /// An ordinary account with anything other than exactly one such membership is refused rather than
    /// signed in without a tenant. Its permissions would be narrowed to a scope it exercises nothing
    /// in, so the session would authenticate it and then refuse everything it went on to do; being
    /// asked which tenant to work in says what to do about that, and a session where nothing works
    /// does not.
    /// <para>
    /// A platform account belongs to no tenant and works platform-wide, so it signs in with none.
    /// </para>
    /// <para>
    /// Two rows answer the whole question - one tenant, or more than one - so no more are read. The read
    /// relaxes tenant restriction by name because it runs before any tenant is established, that being
    /// the very thing it decides; the soft-delete filter stays in force throughout, so a removed
    /// membership places nobody and a deleted tenant is not a tenant to start in.
    /// </para>
    /// </remarks>
    private async Task<Guid?> ResolveUnnamedTenantAsync(User user, CancellationToken cancellationToken)
    {
        var tenantIds = await dbContext.TenantMemberships
            .AsNoTracking()
            .AcrossAllTenants()
            .Where(membership => membership.UserId == user.Id
                                 && dbContext.Tenants.Any(tenant => tenant.Id == membership.TenantId && tenant.Status == TenantStatus.Active))
            .Select(membership => membership.TenantId)
            .Distinct()
            .Take(2)
            .ToListAsync(cancellationToken);

        if (tenantIds.Count == 1)
        {
            return tenantIds[0];
        }

        // A platform account works platform-wide and is never turned away for want of a tenant: with no
        // single membership to start in it starts in none, which is the scope its own authority lives in.
        // An ordinary account has nothing to exercise there, so it is asked which tenant it meant.
        if (!user.IsPlatform)
        {
            ThrowError(x => x.TenantIdentifier, TenantRequiredMessage, ErrorCodes.TenantRequired);
        }

        return null;
    }

    /// <summary>
    /// The tenant named on the request, once it is established that this account may start a session
    /// inside it. Every way it cannot is a refusal rather than a silent fallback: a person who named a
    /// tenant asked for that tenant, and starting them somewhere else - or nowhere - would be a worse
    /// answer than telling them what went wrong.
    /// </summary>
    /// <param name="user">The account that has just authenticated.</param>
    /// <param name="identifier">The tenant's url-safe identifier, as it was typed.</param>
    /// <param name="cancellationToken">Token used to cancel the reads.</param>
    /// <returns>The tenant to start the session in.</returns>
    /// <remarks>
    /// The guards run in a fixed order, and the order is part of the contract: existence first, then
    /// the caller's own standing, then the tenant's lifecycle - the same order
    /// <c>POST /tenants/switch</c> settles the same question in, so naming a tenant here and selecting
    /// it a moment later are refused for the same reasons with the same codes.
    /// <para>
    /// The identifier is matched against the normalized form, so it is found however it was typed.
    /// The read relaxes tenant restriction by name because it runs before
    /// any tenant is established; the soft-delete filter stays in force, so a deleted tenant is not a
    /// tenant to start in and reads as absent.
    /// </para>
    /// </remarks>
    private async Task<Guid?> ResolveNamedTenantAsync(User user, string identifier, CancellationToken cancellationToken)
    {
        var normalizedIdentifier = identifier.Trim().ToLowerInvariant();

        var tenant = await dbContext.Tenants
            .AsNoTracking()
            .AcrossAllTenants()
            .FirstOrDefaultAsync(candidate => candidate.IdentifierNormalized == normalizedIdentifier, cancellationToken);
        if (tenant == null)
        {
            ThrowError(TenantNotFoundMessage, ErrorCodes.TenantNotFound);
        }

        // Read from the membership rows rather than from anything the request carries. The platform
        // tier is the one standing that comes from no membership: it belongs to no tenant and holds in
        // all of them, so a platform account may name any tenant here and start inside it - which is how
        // it reaches a tenant that has reported a problem, without one of that tenant's members having
        // to sign in for it. It is read off the account, because at this point in the request there are
        // no claims to ask: the session being authorized is the one about to be established.
        var holdsMembership = await dbContext.TenantMemberships
            .AsNoTracking()
            .AcrossAllTenants()
            .AnyAsync(membership => membership.TenantId == tenant.Id && membership.UserId == user.Id, cancellationToken);
        if (!holdsMembership && !user.IsPlatform)
        {
            ThrowError(NotTenantMemberMessage, ErrorCodes.NotTenantMember);
        }

        if (tenant.Status == TenantStatus.Suspended)
        {
            ThrowError(TenantSuspendedMessage, ErrorCodes.TenantSuspended);
        }

        return tenant.Id;
    }
}

/// <summary>
/// Request payload for the sign-in/token endpoint, allowing sign-in by either username
/// or email (selected via <see cref="IsEmail"/>) together with the user's password.
/// </summary>
/// <remarks>
/// <see cref="TenantIdentifier"/> is optional and is the tenant's url-safe identifier, never its
/// primary key: it is what the person signing in knows and can type. Left empty, the tenant is
/// resolved from the account's memberships once the credentials are accepted - which settles it when
/// there is exactly one to resolve, and otherwise asks for this field.
/// </remarks>
sealed class TokenRequest
{
    public bool IsEmail { get; set; }
    public string Username { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string Password { get; set; } = null!;
    public string? TenantIdentifier { get; set; }
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
        // Only the length is checked. The shape an identifier must have belongs to the tenancy feature
        // and is not reachable from here, and enforcing it again would buy nothing: an identifier of
        // the wrong shape simply matches no tenant and is refused by the lookup like any other name
        // that names nothing.
        When(x => !string.IsNullOrWhiteSpace(x.TenantIdentifier), () =>
        {
            RuleFor(x => x.TenantIdentifier).MaximumLength(50);
        });
    }
}
