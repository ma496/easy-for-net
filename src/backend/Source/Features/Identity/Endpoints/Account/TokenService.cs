namespace Backend.Features.Identity.Endpoints.Account;

using Backend.Features.Identity.Core;
using Microsoft.Extensions.Options;
using System.Security.Claims;

/// <summary>
/// FastEndpoints refresh-token service that issues and validates access/refresh token
/// pairs for the account sign-in flow, persisting tokens and synchronizing the auth
/// and refresh-token cookies on each issuance.
/// </summary>
public class TokenService : RefreshTokenService<FastEndpoints.Security.TokenRequest, TokenResponse>
{
    private readonly IUserService _userService;
    private readonly IAuthTokenService _authTokenService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly int _refreshTokenValidity;

    /// <summary>
    /// Configures the underlying refresh-token pipeline using the supplied auth settings
    /// and registers a pre-processor that recovers the refresh token from the user's cookie.
    /// </summary>
    public TokenService(IUserService userService,
                       IOptions<AuthSetting> authSetting,
                       IAuthTokenService authTokenService,
                       IHttpContextAccessor httpContextAccessor)
    {
        _userService = userService;
        var authSettingValue = authSetting.Value;
        _authTokenService = authTokenService;
        _httpContextAccessor = httpContextAccessor;
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
    /// this method will be called whenever a new access/refresh token pair is being generated.
    /// store the tokens and expiry dates however you wish for the purpose of verifying
    /// future refresh requests.       
    /// </summary>
    /// <param name="response"></param>
    /// <returns></returns>
    public override async Task PersistTokenAsync(TokenResponse response)
    {
        await _authTokenService.SaveTokenAsync(response);

        // Retrieve claims stored during SetRenewalPrivilegesAsync
        if (_httpContextAccessor.HttpContext?.Items.TryGetValue("CurrentClaims", out var claimsObj) is true &&
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
            _httpContextAccessor.HttpContext?.Response.Cookies.Append("refreshToken", $"{response.UserId}:{response.RefreshToken}", cookieOptions);
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

        if (!await _authTokenService.IsValidRefreshTokenAsync(req))
            ThrowError(r => r.RefreshToken, "Refresh token is invalid!", StatusCodes.Status401Unauthorized);
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

        var roles = await _userService.GetUserRolesAsync(user.Id);
        var permissions = await _userService.GetUserPermissionsAsync(user.Id);
        var claims = Helper.CreateClaims(user, roles, permissions);

        privileges.Claims.AddRange(claims);

        // Store claims in HttpContext items to be used in PersistTokenAsync
        if (_httpContextAccessor.HttpContext != null)
        {
            _httpContextAccessor.HttpContext.Items["CurrentClaims"] = claims;
        }
    }
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

        if (string.IsNullOrEmpty(req.RefreshToken) && httpContext?.Request.Cookies.TryGetValue("refreshToken", out var cookieValue) == true)
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
