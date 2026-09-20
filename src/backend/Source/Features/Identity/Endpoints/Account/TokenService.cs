namespace Backend.Features.Identity.Endpoints.Account;

using Backend.Data.Entities;
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

        // The tenant of the session being renewed, read off the refresh-token row a moment ago, and then
        // asked whether this account may still act in it. This is the one place a live session's tenant is
        // re-examined, and it is what makes suspending a tenant or removing a member an eviction at all:
        // the tenant travels on the refresh-token row and is copied onto its successor, so a renewal that
        // did not ask would hand the same tenant back for as long as the session was refreshed.
        var sessionTenantId = ReadSessionTenant();
        var actingTenantId = await UsableSessionTenantAsync(user, sessionTenantId);

        // Only the claims lose the tenant; the row keeps it, because the recorded tenant is left as it was
        // and PersistTokenAsync writes that one forward. The row records which tenant this session belongs
        // to, which is not the same question as whether it may be acted in today - so a tenant that comes
        // back into service, or a membership that is restored, is picked up by the very next renewal
        // instead of costing the caller a fresh sign-in.
        var grants = await SessionGrants.ReadAsync(_dbContext, user.Id, actingTenantId, user.IsPlatform);

        var claims = Helper.CreateClaims(user, grants.Roles, grants.Permissions, actingTenantId);

        privileges.Claims.AddRange(claims);

        // Store claims in HttpContext items to be used in PersistTokenAsync
        if (_httpContextAccessor.HttpContext != null)
        {
            _httpContextAccessor.HttpContext.Items[CurrentClaimsItemKey] = claims;
        }
    }

    /// <summary>
    /// The tenant the renewed session may act in: the one the expiring session carried, when that tenant
    /// still exists, is not suspended, and the account still belongs to it - and otherwise none.
    /// </summary>
    /// <param name="user">The account the session belongs to.</param>
    /// <param name="tenantId">The tenant the expiring session carried, or <see langword="null"/> for none.</param>
    /// <returns>The tenant to renew into, or <see langword="null"/> when it may no longer be acted in.</returns>
    /// <remarks>
    /// A tenant that may no longer be acted in drops out of the session's claims rather than failing the
    /// renewal. The account keeps its session and is simply left acting in no tenant, which for an ordinary
    /// account is no authority at all, so the web app can offer it another tenant instead of signing it out
    /// over something that was not its doing. The refresh-token row keeps the tenant either way, so a
    /// suspension lifted or a membership restored is picked up by the next renewal.
    /// <para>
    /// Membership is what places an ordinary account inside a tenant, and a platform account holds none
    /// anywhere: it enters a tenant on its tier, so it is admitted on that instead. A suspended or deleted
    /// tenant is refused to it exactly as it is to everyone. The read relaxes tenant restriction by name
    /// because it runs before any scope is established; the soft-delete filter stays in force, which is what
    /// makes a deleted tenant read as absent.
    /// </para>
    /// </remarks>
    private async Task<Guid?> UsableSessionTenantAsync(User user, Guid? tenantId)
    {
        if (tenantId is not { } sessionTenantId)
        {
            return null;
        }

        var isUsable = await _dbContext.Tenants
            .AsNoTracking()
            .AcrossAllTenants()
            .AnyAsync(tenant => tenant.Id == sessionTenantId
                                && tenant.Status == TenantStatus.Active
                                && (user.IsPlatform
                                    || _dbContext.TenantMemberships.AcrossAllTenants()
                                        .Any(membership => membership.TenantId == sessionTenantId && membership.UserId == user.Id)));

        return isUsable ? sessionTenantId : null;
    }

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
