namespace Backend.Features.Identity.Endpoints.Account;

using Backend.Features.Identity.Core;

/// <summary>
/// Anonymous POST endpoint that authenticates a user by username/email and password and
/// issues a JWT access/refresh token pair along with a refresh-token cookie.
/// </summary>
sealed class TokenEndpoint(IUserService userService, IOptions<SigninSetting> signinSetting, IOptions<AuthSetting> authSetting) : Endpoint<TokenRequest, TokenResponse>
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
        var errorMessage = !req.IsEmail ? "Username or password is invalid" : "Email or password is invalid";
        var errorCode = !req.IsEmail ? ErrorCodes.InvalidUsernamePassword : ErrorCodes.InvalidEmailPassword;
        if (user == null)
            ThrowError(errorMessage, errorCode);

        var result = await userService.ValidatePasswordAsync(user, req.Password);
        if (!result)
            ThrowError(errorMessage, errorCode);
        if (!user.IsActive)
            ThrowError("User is not active", ErrorCodes.UserNotActive);
        if (signinSetting.Value?.IsEmailVerificationRequired == true && !user.IsEmailVerified)
            ThrowError("Email is not verified", ErrorCodes.EmailNotVerified);

        var roles = await userService.GetUserRolesAsync(user.Id);
        var permissions = await userService.GetUserPermissionsAsync(user.Id);

        var claims = Helper.CreateClaims(user, roles, permissions);

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
