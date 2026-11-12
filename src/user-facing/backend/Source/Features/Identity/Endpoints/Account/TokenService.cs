using Backend.Features.Identity.Core;
using Backend.Features.Identity.Core.Entities;
using Backend.Features.Identity.Core.Sessions;
using Backend.Features.Settings.Core;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace Backend.Features.Identity.Endpoints.Account;

/// <summary>
/// FastEndpoints refresh-token service that issues and validates access/refresh token
/// pairs for the account sign-in flow, persisting tokens and synchronizing the auth
/// and refresh-token cookies on each issuance.
/// </summary>
/// <remarks>
/// The pair carries only the account and the session's identifier; the roles and permissions live in
/// the session store.
/// </remarks>
public class TokenService : RefreshTokenService<FastEndpoints.Security.TokenRequest, TokenResponse>
{
    /// <summary>
    /// Name of the cookie a browser client carries its refresh token in, as <c>UserId:RefreshToken</c>.
    /// It is named here once because three places read or write it: the issuance that sets it, the
    /// pre-processor that recovers a refresh request from it, and the sign-out that clears it.
    /// </summary>
    public const string RefreshTokenCookieName = "refreshToken";

    /// <summary>
    /// Key under which the claims built for a renewal are carried, so that the cookie principal is re-signed
    /// with exactly the claims the new access token carries.
    /// </summary>
    private const string CurrentClaimsItemKey = "CurrentClaims";

    /// <summary>
    /// Key under which the session-store record minted for the pair being issued is carried across the
    /// steps of one issuance, so the refresh-token row written for it names the session it belongs to.
    /// </summary>
    private const string SessionIdItemKey = "Backend.Features.Identity.SessionId";

    /// <summary>
    /// Key under which the account's security stamp the pair being issued was authorized under is carried
    /// to the row written for it: the stamp read with the password at sign-in, and off the consumed row at a
    /// refresh.
    /// </summary>
    private const string SecurityStampItemKey = "Backend.Features.Identity.SecurityStamp";

    /// <summary>
    /// Key under which the session a refresh replaces is carried from validating the request, which is
    /// where the consumed row is read, to building the renewed session, which is where it is deleted.
    /// </summary>
    private const string ReplacedSessionIdItemKey = "Backend.Features.Identity.ReplacedSessionId";

    private readonly IUserService _userService;
    private readonly IAuthTokenService _authTokenService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AppDbContext _dbContext;
    private readonly ISettingProvider _settingProvider;
    private readonly ISessionIssuer _sessionIssuer;
    private readonly ISessionStore _sessionStore;
    private readonly int _refreshTokenValidity;

    /// <summary>
    /// Configures the underlying refresh-token pipeline using the supplied auth settings
    /// and registers a pre-processor that recovers the refresh token from the user's cookie.
    /// </summary>
    public TokenService(IUserService userService,
                       IOptions<AuthSetting> authSetting,
                       ISettingProvider settingProvider,
                       IAuthTokenService authTokenService,
                       IHttpContextAccessor httpContextAccessor,
                       AppDbContext dbContext,
                       ISessionIssuer sessionIssuer,
                       ISessionStore sessionStore)
    {
        _userService = userService;
        var authSettingValue = authSetting.Value;
        _authTokenService = authTokenService;
        _httpContextAccessor = httpContextAccessor;
        _dbContext = dbContext;
        _sessionIssuer = sessionIssuer;
        _sessionStore = sessionStore;
        _settingProvider = settingProvider;
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
    /// Records, for the request being handled, the session-store record the pair about to be issued belongs
    /// to and the account's security stamp it was authorized under, so the refresh-token row written for it
    /// names that session and carries that stamp. Every issuance point calls this after minting the session
    /// and before asking for the pair.
    /// </summary>
    /// <param name="httpContext">The request the pair is being issued on.</param>
    /// <param name="sessionId">The session the pair belongs to.</param>
    /// <param name="securityStamp">
    /// The account's <c>SecurityStamp</c> as it stood when the issuance was authorized - read together with
    /// the credentials or the row it was authorized on, never re-read afterwards.
    /// </param>
    public static void RecordSessionId(HttpContext httpContext, string sessionId, Guid securityStamp)
    {
        httpContext.Items[SessionIdItemKey] = sessionId;
        httpContext.Items[SecurityStampItemKey] = securityStamp;
    }

    /// <summary>
    /// this method will be called whenever a new access/refresh token pair is being generated.
    /// store the tokens and expiry dates however you wish for the purpose of verifying
    /// future refresh requests.
    /// </summary>
    /// <param name="response"></param>
    /// <returns></returns>
    public override async Task PersistTokenAsync(TokenResponse response)
    {
        var securityStamp = ReadSecurityStamp()
            ?? throw new InvalidOperationException("A token pair is issued only after its session and security stamp have been recorded.");
        var sessionId = ReadItem(SessionIdItemKey);
        var saved = await _authTokenService.SaveTokenAsync(response, sessionId, securityStamp);

        // The stamp is read again now that the row and the session both exist. A credential change that
        // committed after this issuance was authorized - a password reset racing a refresh - may have swept
        // the account's rows and sessions before these two were written, so they are withdrawn and the
        // issuance refused. When the stamp still matches, both were stored before any such change
        // committed, and the revocation that follows it reaches them.
        var currentStamp = await _dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == saved.UserId)
            .Select(user => (Guid?)user.SecurityStamp)
            .FirstOrDefaultAsync();
        if (currentStamp != securityStamp)
        {
            await _authTokenService.RevokeRefreshTokenAsync(saved.UserId, response.RefreshToken);
            if (sessionId is not null)
                await _sessionStore.DeleteAsync(sessionId);
            ThrowError("The account's credentials changed while the session was being issued.", StatusCodes.Status401Unauthorized);
        }

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

        // The consumed row has just been deleted, so what it carried is put on the request for the steps
        // that follow: the renewed session, and the row written for the new refresh token.
        if (_httpContextAccessor.HttpContext is { } httpContext)
        {
            httpContext.Items[ReplacedSessionIdItemKey] = consumption.SessionId;
            httpContext.Items[SecurityStampItemKey] = consumption.SecurityStamp;
        }
    }

    /// <summary>
    /// mint the renewed session and name it in the jwt when a refresh request is
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
            this.ThrowError(r => r.UserId, ErrorCodes.UserNotFound);
        if (!user.IsActive)
            this.ThrowError(r => r.UserId, ErrorCodes.UserNotActive);
        // A row issued under credentials that have since changed renews nothing: a password change or reset
        // rotates the stamp, so a refresh token that outlived the revocation - written by a renewal racing
        // it - is refused here at its next use.
        // The session being renewed goes with the refusal: its row is already consumed, so a revocation
        // that looked for it through the rows can no longer find it.
        if (ReadSecurityStamp() != user.SecurityStamp)
        {
            if (ReadItem(ReplacedSessionIdItemKey) is { } refusedSessionId)
                await _sessionStore.DeleteAsync(refusedSessionId);
            ThrowError(r => r.RefreshToken, "Refresh token is invalid!", StatusCodes.Status401Unauthorized);
        }

        // Held to the sign-in setting as it stands now, so a change to the setting reaches a live session
        // at its next renewal without revoking it.
        var generalSettings = await _settingProvider.GetAsync<GeneralSettings>();
        if (generalSettings.IsEmailVerificationRequired && !user.IsEmailVerified)
            this.ThrowError(r => r.UserId, ErrorCodes.EmailNotVerified);

        var session = await _sessionIssuer.IssueAsync(user);

        // The renewal replaces the session it renews: the old one is deleted once its successor exists, so
        // an access token still naming it stops working at once. The refresh-token row was already consumed
        // when the request was validated, so a refresh during a session store outage answers 503 and uses
        // up the refresh token: the caller has to sign in again.
        if (ReadItem(ReplacedSessionIdItemKey) is { } replacedSessionId)
            await _sessionStore.DeleteAsync(replacedSessionId);

        // The token and the cookie carry the account and the session and nothing else; what the session
        // grants is read from the store on every request.
        var claims = SessionClaims.ForToken(user.Id, session.SessionId);

        privileges.Claims.AddRange(claims);

        // Store claims in HttpContext items to be used in PersistTokenAsync
        if (_httpContextAccessor.HttpContext != null)
        {
            _httpContextAccessor.HttpContext.Items[CurrentClaimsItemKey] = claims;
            RecordSessionId(_httpContextAccessor.HttpContext, session.SessionId, user.SecurityStamp);
        }
    }

    /// <summary>
    /// Reads the security stamp recorded for the request being handled, or <see langword="null"/> when none was.
    /// </summary>
    private Guid? ReadSecurityStamp()
        => _httpContextAccessor.HttpContext?.Items.TryGetValue(SecurityStampItemKey, out var recorded) is true && recorded is Guid stamp
            ? stamp
            : null;

    /// <summary>
    /// Reads a string recorded for the request being handled, or <see langword="null"/> when none was.
    /// </summary>
    /// <param name="key">The <see cref="HttpContext.Items"/> key it was recorded under.</param>
    private string? ReadItem(string key)
        => _httpContextAccessor.HttpContext?.Items.TryGetValue(key, out var recorded) is true ? recorded as string : null;
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
