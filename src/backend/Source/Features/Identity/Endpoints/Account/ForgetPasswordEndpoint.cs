namespace Backend.Features.Identity.Endpoints.Account;

using Backend.External.Email;
using Backend.Features.Identity.Core;
using Backend.Features.Identity.Core.Entities;
using Backend.Settings;
using Microsoft.Extensions.Options;

/// <summary>
/// Anonymous POST endpoint that initiates the password-reset flow by emailing a reset
/// token to the requesting user.
/// </summary>
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
        var user = await userService.GetByEmailAsync(request.Email);
        if (user == null)
        {
            // Return success even if email doesn't exist to prevent email enumeration
            await Send.OkAsync(cancellationToken);
            return;
        }

        // Generate reset token
        var resetToken = await tokenService.GenerateTokenAsync(user, TokenPurpose.PasswordReset);

        // Send email with reset token
        emailBackgroundJobs.Enqueue(user.Email, "Reset Password",
            @$"
            <div>
                <p>Click the link below to reset your password:</p>
                <a href=""{webSetting.Value.DefaultDomain}/reset-password?token={resetToken.Value}"">Reset Password</a>
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
