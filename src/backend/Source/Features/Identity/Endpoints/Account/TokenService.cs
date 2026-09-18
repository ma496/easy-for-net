namespace Backend.Features.Identity.Endpoints.Account;

using Backend.Features.Identity.Core;
using Backend.Features.Identity.Core.Entities;
using Microsoft.Extensions.Options;
using System.Security.Claims;

/// <summary>
/// FastEndpoints refresh-token service that issues and validates access/refresh token
/// pairs for the account sign-in flow, persisting tokens and synchronizing the auth
/// and refresh-token cookies on each issuance.
/// </summary>
/// <remarks>
/// The tenant a session acts in travels with the token pair rather than with the claims of the
/// session being replaced: it is recorded on the refresh-token row when the session is established
/// or switched, read back when that row is consumed, and written forward onto the row issued in its
/// place. A refresh therefore re-establishes the very tenant the session already had - it can
/// neither lose it as the session is renewed nor resurrect the one the user acted in before
/// switching - and the roles and permissions it embeds are recomputed for that tenant from the
/// membership and role assignments as they stand at that moment.
/// </remarks>
public class TokenService : RefreshTokenService<FastEndpoints.Security.TokenRequest, TokenResponse>
{
    /// <summary>
    /// Key under which the tenant of the session being issued is carried across the three steps of one
    /// issuance - validating the refresh request, building the claims, and persisting the new pair - because
    /// the framework hands those steps nothing but the user identifier.
    /// </summary>
    private const string SessionTenantItemKey = "Backend.Tenancy.SessionTenant";

    /// <summary>
    /// Name of the cookie a browser client carries its refresh token in, as <c>UserId:RefreshToken</c>.
    /// It is named here once because three places read or write it: the issuance that sets it, the
    /// pre-processor that recovers a refresh request from it, and the tenant switch that revokes the
    /// pair it names before issuing the one that replaces it.
    /// </summary>
    public const string RefreshTokenCookieName = "refreshToken";

    /// <summary>
    /// Key under which the claims built for a renewal are carried, so that the cookie principal is re-signed
    /// with exactly the claims the new access token carries.
    /// </summary>
    private const string CurrentClaimsItemKey = "CurrentClaims";

    /// <summary>
    /// Authentication type of the principal the renewed session is evaluated as. It never authenticates a
    /// request; it only names the identity built to ask what that session is entitled to.
    /// </summary>
    private const string RenewalAuthenticationType = "RefreshTokenRenewal";

    private readonly IUserService _userService;
    private readonly IAuthTokenService _authTokenService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AppDbContext _dbContext;
    private readonly SigninSetting _signinSetting;
    private readonly int _refreshTokenValidity;

    /// <summary>
    /// Configures the underlying refresh-token pipeline using the supplied auth settings
    /// and registers a pre-processor that recovers the refresh token from the user's cookie.
    /// </summary>
    public TokenService(IUserService userService,
                       IOptions<AuthSetting> authSetting,
                       IOptions<SigninSetting> signinSetting,
                       IAuthTokenService authTokenService,
                       IHttpContextAccessor httpContextAccessor,
                       AppDbContext dbContext)
    {
        _userService = userService;
        var authSettingValue = authSetting.Value;
        _authTokenService = authTokenService;
        _httpContextAccessor = httpContextAccessor;
        _dbContext = dbContext;
        _signinSetting = signinSetting.Value;
        _refreshTokenValidity = authSettingValue.RefreshTokenValidity;

        Setup(o =>
        {
            o.TokenSigningKey = authSettingValue.Jwt.Key;
            o.Issuer = authSettingValue.Jwt.Issuer;
            o.Audience = authSettingValue.Jwt.Audience;
            o.AccessTokenValidity = TimeSpan.FromMinutes(authSettingValue.AccessTokenValidity);
            o.RefreshTokenValidity = TimeSpan.FromHours(authSettingValue.RefreshTokenValidity);

            o.Endpoint("account/refresh-token", ep =>
            {
                // Register the pre-processor to handle cookie-based tokens
                ep.PreProcessors(Order.Before, new RefreshTokenPreProcessor());
            });
        });
    }

    /// <summary>
    /// Records, for the request being handled, the tenant the session about to be issued acts in, so that the
    /// refresh-token row written for it carries that tenant and every later refresh re-establishes the same
    /// one. Sign-in and tenant selection call this before asking for a token pair; a request that records
    /// nothing issues a session that acts in no tenant, which is the state a user with no active membership,
    /// or with several and no choice made between them, is in.
    /// </summary>
    /// <param name="httpContext">The request the session is being issued on.</param>
    /// <param name="tenantId">The tenant the session acts in, or <see langword="null"/> for none.</param>
    public static void RecordSessionTenant(HttpContext httpContext, Guid? tenantId)
        => httpContext.Items[SessionTenantItemKey] = tenantId;

    /// <summary>
    /// this method will be called whenever a new access/refresh token pair is being generated.
    /// store the tokens and expiry dates however you wish for the purpose of verifying
    /// future refresh requests.
    /// </summary>
    /// <param name="response"></param>
    /// <returns></returns>
    public override async Task PersistTokenAsync(TokenResponse response)
    {
        // The tenant is written onto the row alongside the tokens, because that row is all a later refresh
        // has to go on: the access token it renews has expired by then.
        await _authTokenService.SaveTokenAsync(response, ReadSessionTenant());

        // Retrieve claims stored during SetRenewalPrivilegesAsync
        if (_httpContextAccessor.HttpContext?.Items.TryGetValue(CurrentClaimsItemKey, out var claimsObj) is true &&
            claimsObj is List<Claim> claims)
        {
            // Refresh the auth cookie (access token session)
            await CookieAuth.SignInAsync(u =>
            {
                u.Claims.AddRange(claims);
            });
        }

        // Update the refresh token cookie
        if (_httpContextAccessor.HttpContext != null)
        {
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = _httpContextAccessor.HttpContext?.Request.IsHttps ?? false,
                SameSite = SameSiteMode.Strict,
                Expires = DateTime.UtcNow.AddHours(_refreshTokenValidity)
            };
            // Embed UserId in the cookie so we can recover it when the session expires
            _httpContextAccessor.HttpContext?.Response.Cookies.Append(RefreshTokenCookieName, $"{response.UserId}:{response.RefreshToken}", cookieOptions);
        }
    }

    /// <summary>
    /// validate the incoming refresh request by checking the token and expiry against the
    /// previously stored data. if the token is not valid and a new token pair should
    /// not be created, simply add validation errors using the AddError() method.
    /// the failures you add will be sent to the requesting client. if no failures are added,
    /// validation passes and a new token pair will be created and sent to the client.
    /// </summary>
    /// <param name="req"></param>
    /// <returns></returns>
    public override async Task RefreshRequestValidationAsync(FastEndpoints.Security.TokenRequest req)
    {
        // If no UserId or RefreshToken (e.g. cookie missing/invalid), fail immediately
        if (string.IsNullOrEmpty(req.UserId) || string.IsNullOrEmpty(req.RefreshToken))
            ThrowError(r => r.RefreshToken, "Refresh token is missing or invalid!", StatusCodes.Status401Unauthorized);

        var consumption = await _authTokenService.ConsumeRefreshTokenAsync(req);
        if (!consumption.Consumed)
            ThrowError(r => r.RefreshToken, "Refresh token is invalid!", StatusCodes.Status401Unauthorized);

        // The consumed row has just been deleted, so the tenant it carried is put on the request for the two
        // steps that follow: the claims the renewed session is built from, and the row written for the new
        // refresh token. Taking it from there rather than from the caller is what stops a refresh from
        // honouring a tenant the request asserts for itself.
        if (_httpContextAccessor.HttpContext is { } httpContext)
            RecordSessionTenant(httpContext, consumption.TenantId);
    }

    /// <summary>
    /// specify the user privileges to be embedded in the jwt when a refresh request is
    /// received and validation has passed. this only applies to renewal/refresh requests
    /// received to the refresh endpoint and not the initial jwt creation.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="privileges"></param>
    /// <returns></returns>
    public override async Task SetRenewalPrivilegesAsync(FastEndpoints.Security.TokenRequest request, UserPrivileges privileges)
    {
        var user = await _userService.GetByIdAsync(Guid.Parse(request.UserId));
        if (user == null)
            ThrowError(r => r.UserId, "User not found", ErrorCodes.UserNotFound);
        if (!user.IsActive)
            ThrowError(r => r.UserId, "User is not active", ErrorCodes.UserNotActive);
        if (_signinSetting.IsEmailVerificationRequired && !user.IsEmailVerified)
            ThrowError(r => r.UserId, "Email is not verified", ErrorCodes.EmailNotVerified);

        // The tenant of the session being renewed, read off the refresh-token row a moment ago.
        var tenantId = ReadSessionTenant();

        // What the renewed session may do is read from current data for that tenant alone, never copied from
        // the session it replaces: a membership revoked, a role assignment replaced or a tenant suspended
        // since the session began takes effect here, and the grants of a tenant the user acted in earlier are
        // not carried into the one they act in now.
        var session = await SessionValidator.EvaluateAsync(RenewalPrincipal(user, tenantId), _dbContext);

        // The tenant claim is issued from the row even when its tenant may no longer be acted in - the grants
        // above are then empty - so that the refusal the next request meets names that tenant and the reason
        // for it, rather than reporting that no tenant was ever selected.
        var claims = Helper.CreateClaims(user, [.. session.Roles], [.. session.Permissions], tenantId);

        privileges.Claims.AddRange(claims);

        // Store claims in HttpContext items to be used in PersistTokenAsync
        if (_httpContextAccessor.HttpContext != null)
        {
            _httpContextAccessor.HttpContext.Items[CurrentClaimsItemKey] = claims;
        }
    }

    /// <summary>
    /// Builds the principal the renewed session will carry, holding the account's identity and the tenant it
    /// acts in and nothing else, so that the session check answers what this session is entitled to now
    /// rather than what the expiring one was granted when it began.
    /// </summary>
    /// <param name="user">The account the session belongs to.</param>
    /// <param name="tenantId">The tenant the session acts in, or <see langword="null"/> for none.</param>
    /// <returns>A principal carrying identity and tenant claims only.</returns>
    private static ClaimsPrincipal RenewalPrincipal(User user, Guid? tenantId)
        => new(new ClaimsIdentity(Helper.CreateClaims(user, [], [], tenantId), RenewalAuthenticationType));

    /// <summary>
    /// Reads the tenant recorded for the request being handled. A request that recorded none - an ordinary
    /// sign-in by a user who belongs to no tenant, or who has not chosen between several - issues a session
    /// that acts in no tenant.
    /// </summary>
    /// <returns>The recorded tenant, or <see langword="null"/> when none was recorded.</returns>
    private Guid? ReadSessionTenant()
        => _httpContextAccessor.HttpContext?.Items.TryGetValue(SessionTenantItemKey, out var recorded) is true && recorded is Guid tenantId
            ? tenantId
            : null;
}

/// <summary>
/// Global pre-processor that recovers refresh-token information from the
/// <c>refreshToken</c> cookie when it is not provided directly on the request, also
/// falling back to the authenticated user's identifier when needed.
/// </summary>
public class RefreshTokenPreProcessor : IGlobalPreProcessor
{
    public Task PreProcessAsync(IPreProcessorContext context, CancellationToken ct)
    {
        if (context.Request is not FastEndpoints.Security.TokenRequest req)
            return Task.CompletedTask;

        var httpContext = context.HttpContext;

        if (string.IsNullOrEmpty(req.RefreshToken) && httpContext?.Request.Cookies.TryGetValue(TokenService.RefreshTokenCookieName, out var cookieValue) == true)
        {
            // Try to parse "UserId:RefreshToken" format
            var parts = cookieValue!.Split(':');
            if (parts.Length == 2)
            {
                req.UserId = parts[0];
                req.RefreshToken = parts[1];
            }
            else
            {
                // Fallback for legacy cookies or simple token (though UserId will likely be missing)
                req.RefreshToken = cookieValue;
            }

            // Fallback: If UserId is still missing, try to extract from authenticated user principal
            if (string.IsNullOrEmpty(req.UserId) && httpContext.User.Identity?.IsAuthenticated == true)
            {
                var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                             ?? httpContext.User.FindFirst("sub")?.Value;
                if (userId != null) req.UserId = userId;
            }
        }
        return Task.CompletedTask;
    }
}
