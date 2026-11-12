using Backend.External.Email;
using Backend.Features.Identity.Core;
using Backend.Features.Identity.Core.Entities;
using Backend.Settings;

namespace Backend.Features.Identity.Endpoints.Account;

/// <summary>
/// Anonymous POST endpoint that initiates the password-reset flow by emailing a reset
/// token to the requesting user.
/// </summary>
/// <remarks>
/// The emailed link points at the site origin, <c>Web:DefaultDomain</c>.
/// </remarks>
sealed class ForgetPasswordEndpoint(ITokenService tokenService,
                                    IUserService userService,
                                    IEmailBackgroundJobs emailBackgroundJobs,
                                    IOptions<WebSetting> webSetting)
    : Endpoint<ForgetPasswordRequest, EmptyResponse>
{
    public override void Configure()
    {
        Post("forget-password");
        Group<AccountGroup>();
        AllowAnonymous();
    }

    public override async Task HandleAsync(ForgetPasswordRequest request, CancellationToken cancellationToken)
    {
        // Only the identifier the token is issued to and the address it is mailed to are read.
        var normalizedEmail = request.Email.ToLowerInvariant();
        var user = await userService.Users()
            .AsNoTracking()
            .Where(u => u.EmailNormalized == normalizedEmail)
            .Select(u => new { u.Id, u.Email })
            .FirstOrDefaultAsync(cancellationToken);
        if (user == null)
        {
            // Return success even if email doesn't exist to prevent email enumeration
            await Send.OkAsync(cancellationToken);
            return;
        }

        // Generate reset token
        var resetToken = await tokenService.GenerateTokenAsync(user.Id, TokenPurpose.PasswordReset);

        // Send email with reset token
        emailBackgroundJobs.Enqueue(user.Email, "Reset Password",
            @$"
            <div>
                <p>Click the link below to reset your password:</p>
                <a href=""{webSetting.Value.SiteOrigin()}/reset-password?token={resetToken.Value}"">Reset Password</a>
            </div>", true);

        await Send.OkAsync(cancellationToken);
    }
}

/// <summary>
/// Request payload for initiating a password reset, containing the user's email address.
/// </summary>
sealed class ForgetPasswordRequest
{
    public string Email { get; set; } = null!;
}

/// <summary>
/// FluentValidation rules for <see cref="ForgetPasswordRequest"/>, ensuring a valid,
/// non-empty email address of reasonable length is supplied.
/// </summary>
sealed class ForgetPasswordValidator : Validator<ForgetPasswordRequest>
{
    public ForgetPasswordValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(100);
    }
}
