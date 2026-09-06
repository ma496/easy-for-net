namespace Backend.Features.Identity.Endpoints.Account;

using Backend.External.Email;
using Backend.Features.Identity.Core;
using Backend.Features.Identity.Core.Entities;
using Backend.Settings;
using Microsoft.Extensions.Options;

/// <summary>
/// Anonymous POST endpoint that re-issues an email verification message to a user who
/// has not yet verified their email address.
/// </summary>
sealed class ResendVerifyEmailEndpoint(IUserService userService,
                               ITokenService tokenService,
                               IEmailBackgroundJobs emailBackgroundJobs,
                               IOptions<WebSetting> webSetting,
                               IOptions<SigninSetting> signinSetting)
    : Endpoint<ResendVerifyEmailRequest, EmptyResponse>
{
    public override void Configure()
    {
        Post("resend-verify-email");
        Group<AccountGroup>();
        AllowAnonymous();
    }

    public override async Task HandleAsync(ResendVerifyEmailRequest request, CancellationToken cancellationToken)
    {
        if (!signinSetting.Value.IsEmailVerificationRequired)
        {
            await Send.OkAsync(cancellationToken);
            return;
        }

        var user = await userService.GetByEmailOrUsernameAsync(request.EmailOrUsername);
        if (user == null)
        {
            // For security reasons, don't reveal that the user doesn't exist
            await Send.OkAsync(cancellationToken);
            return;
        }

        if (user.IsEmailVerified)
        {
            await Send.OkAsync(cancellationToken);
            return;
        }

        // Generate verification token
        var token = await tokenService.GenerateTokenAsync(user, TokenPurpose.EmailVerification);

        // Send verification email
        emailBackgroundJobs.Enqueue(user.Email, "Verify Email",
            @$"
            <div>
                <p>Click the link below to verify your email:</p>
                <a href=""{webSetting.Value.DefaultDomain}/verify-email?token={token.Value}"">Verify Email</a>
            </div>", true);

        await Send.OkAsync(cancellationToken);
    }
}

/// <summary>
/// Request payload for resending an email verification, accepting either the user's
/// email or username as a single identifier.
/// </summary>
sealed class ResendVerifyEmailRequest
{
    public string EmailOrUsername { get; set; } = null!;
}

/// <summary>
/// FluentValidation rules for <see cref="ResendVerifyEmailRequest"/>, requiring that
/// the email-or-username field is supplied.
/// </summary>
sealed class ResendVerifyEmailValidator : Validator<ResendVerifyEmailRequest>
{
    public ResendVerifyEmailValidator()
    {
        RuleFor(x => x.EmailOrUsername)
            .NotEmpty();
    }
}
