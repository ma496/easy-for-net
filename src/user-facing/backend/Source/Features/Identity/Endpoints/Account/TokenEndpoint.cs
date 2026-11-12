using Backend.Features.Identity.Core;
using Backend.Features.Identity.Core.Sessions;
using Backend.Features.Settings.Core;

namespace Backend.Features.Identity.Endpoints.Account;

/// <summary>
/// Anonymous POST endpoint that authenticates a user by username/email and password and
/// issues a JWT access/refresh token pair along with a refresh-token cookie.
/// </summary>
sealed class TokenEndpoint(IUserService userService,
                           ISessionIssuer sessionIssuer,
                           ISettingProvider settingProvider,
                           IOptions<AuthSetting> authSetting) : Endpoint<TokenRequest, TokenResponse>
{
    public override void Configure()
    {
        Post("token");
        Group<AccountGroup>();
        AllowAnonymous();
    }

    public override async Task HandleAsync(TokenRequest req, CancellationToken c)
    {
        var user = await (!req.IsEmail ? userService.GetByUsernameAsync(req.Username) : userService.GetByEmailAsync(req.Email));
        var errorCode = !req.IsEmail ? ErrorCodes.InvalidUsernamePassword : ErrorCodes.InvalidEmailPassword;
        if (user == null)
            this.ThrowError(errorCode);

        var result = await userService.ValidatePasswordAsync(user, req.Password);
        if (!result)
            this.ThrowError(errorCode);

        if (!user.IsActive)
            this.ThrowError(ErrorCodes.UserNotActive);

        var generalSettings = await settingProvider.GetAsync<GeneralSettings>(c);
        if (generalSettings.IsEmailVerificationRequired && !user.IsEmailVerified)
            this.ThrowError(ErrorCodes.EmailNotVerified);

        // The session is what every request made with the token below is authorized on, until it is
        // renewed, revoked or replaced by a new sign-in. The token and the cookie carry only the account
        // and the session's identifier.
        var session = await sessionIssuer.IssueAsync(user, c);
        TokenService.RecordSessionId(HttpContext, session.SessionId, user.SecurityStamp);

        var claims = SessionClaims.ForToken(user.Id, session.SessionId);

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
}

/// <summary>
/// Request payload for the sign-in/token endpoint, allowing sign-in by either username
/// or email (selected via <see cref="IsEmail"/>) together with the user's password.
/// </summary>
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
