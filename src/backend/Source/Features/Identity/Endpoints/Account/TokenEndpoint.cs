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
/// starts in is decided here, after authentication, and never from the credentials themselves. The
/// endpoint is therefore exempt from the active-tenant requirement itself: it is one of the places a
/// tenant is established.
/// <para>
/// The request may name a tenant beside the credentials, and that is a convenience rather than a
/// second credential: it saves a person who belongs to several tenants the detour through the chooser
/// by stating up front which one they came to work in. It is authorized here on exactly the standing
/// that <c>POST /tenants/switch</c> would authorize it on a moment later, and a tenant the account
/// cannot act in refuses the sign-in outright rather than quietly starting a session somewhere else:
/// a person who named a tenant is told why they did not get it.
/// </para>
/// <para>
/// With no tenant named the session starts where it always has - from the memberships the account
/// holds at that moment. Exactly one active membership starts the session inside that tenant with
/// nothing for the user to choose, while none and several alike start a session that acts in no
/// tenant, leaving every tenant-scoped operation refused until a tenant is selected.
/// </para>
/// </remarks>
[AllowNoTenant]
sealed class TokenEndpoint(IUserService userService, AppDbContext dbContext, IOptions<SigninSetting> signinSetting, IOptions<AuthSetting> authSetting) : Endpoint<TokenRequest, TokenResponse>
{
    /// <summary>
    /// Authentication type of the principal the session being established is evaluated as. It never
    /// authenticates a request; it only names the identity built to ask what this session is entitled to.
    /// </summary>
    private const string SigninAuthenticationType = "Signin";

    /// <summary>
    /// The refusal reported for a tenant identifier that names no tenant, or names a deleted one. It
    /// carries no detail, so the answer for a tenant that never existed and the answer for one that is
    /// gone are the same answer.
    /// </summary>
    private const string TenantNotFoundMessage = "Tenant not found";

    /// <summary>
    /// The refusal reported when the account holds no active membership in the tenant it named.
    /// Holding permissions - even every permission - in another tenant is not standing in this one;
    /// only platform administration, which belongs to no tenant at all, is.
    /// </summary>
    private const string NotTenantMemberMessage = "You are not a member of this tenant";

    /// <summary>
    /// The refusal reported when the tenant named is suspended. A suspended tenant is out of service
    /// rather than gone, so signing in to work inside it is refused while it is.
    /// </summary>
    private const string TenantSuspendedMessage = "The tenant is suspended";

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
            ? await ResolveSingleActiveTenantAsync(user.Id, c)
            : await ResolveNamedTenantAsync(user, req.TenantIdentifier, c);

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
    /// The identifier is matched against the normalized form, so it is found however it was typed -
    /// the normalization repeated here is the one <see cref="Tenant.NormalizeProperties"/> performs
    /// when the tenant is stored. The read relaxes tenant restriction by name because it runs before
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

        // Read from the membership rows rather than from anything the account carries. Platform
        // administration is the one standing that comes from no membership: it belongs to no tenant and
        // holds in all of them, so a platform administrator may name any tenant here and start inside
        // it - which is how they reach a tenant that has reported a problem, without one of its members
        // having to sign in for them. It is read from the account's platform-scoped roles, because at
        // this point in the request there are no permission claims to ask: the session being
        // authorized is the one about to be established.
        var holdsMembership = await dbContext.TenantMemberships
            .AsNoTracking()
            .AcrossAllTenants()
            .AnyAsync(membership => membership.TenantId == tenant.Id && membership.UserId == user.Id, cancellationToken);
        if (!holdsMembership && !await HoldsPlatformAdministrationAsync(user.Id, cancellationToken))
        {
            ThrowError(NotTenantMemberMessage, ErrorCodes.NotTenantMember);
        }

        if (tenant.Status == TenantStatus.Suspended)
        {
            ThrowError(TenantSuspendedMessage, ErrorCodes.TenantSuspended);
        }

        return tenant.Id;
    }

    /// <summary>
    /// Whether the account holds platform administration through a platform-scoped role - one
    /// belonging to no tenant. Asked of the roles rather than of a permission the account holds
    /// anywhere, because a role belonging to a tenant can never confer this, and so no tenant can mint
    /// for itself the authority to be signed into from outside.
    /// </summary>
    /// <param name="userId">The account that has just authenticated.</param>
    /// <param name="cancellationToken">Token used to cancel the read.</param>
    /// <returns><see langword="true"/> when the account is a platform administrator.</returns>
    private async Task<bool> HoldsPlatformAdministrationAsync(Guid userId, CancellationToken cancellationToken)
        => await dbContext.Roles
            .AsNoTracking()
            .AcrossAllTenants()
            .AnyAsync(role => role.TenantId == null
                              && role.RolePermissions.Any(rolePermission => rolePermission.Permission.Name == Allow.Platform_Administration)
                              && dbContext.UserRoles.Any(assignment => assignment.UserId == userId && assignment.RoleId == role.Id),
                      cancellationToken);

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
/// <see cref="TenantIdentifier"/> is optional and is the tenant's url-safe identifier, never its
/// primary key: it is what the person signing in knows and can type. Left empty - which is how every
/// sign-in that does not care arrives - the tenant is resolved from the account's memberships once
/// the credentials are accepted, exactly as it always was.
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
