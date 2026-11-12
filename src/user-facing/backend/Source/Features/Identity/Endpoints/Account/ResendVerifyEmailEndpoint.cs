using Backend.External.Email;
using Backend.Features.Identity.Core;
using Backend.Features.Identity.Core.Entities;
using Backend.Features.Settings.Core;
using Backend.Settings;

namespace Backend.Features.Identity.Endpoints.Account;

/// <summary>
/// Anonymous POST endpoint that re-issues an email verification message to a user who
/// has not yet verified their email address.
/// </summary>
/// <remarks>
/// The emailed link points at the site origin, <c>Web:DefaultDomain</c>.
/// </remarks>
sealed class ResendVerifyEmailEndpoint(IUserService userService,
                               ITokenService tokenService,
                               IEmailBackgroundJobs emailBackgroundJobs,
                               IOptions<WebSetting> webSetting,
                               ISettingProvider settingProvider)
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
        var generalSettings = await settingProvider.GetAsync<GeneralSettings>(cancellationToken);
        if (!generalSettings.IsEmailVerificationRequired)
        {
            await Send.OkAsync(cancellationToken);
            return;
        }

        // Only what this flow reads: the account to issue the token to, its address, and whether it is
        // already verified.
        var emailOrUsername = request.EmailOrUsername.ToLowerInvariant();
        var user = await userService.Users()
            .AsNoTracking()
            .Where(u => u.EmailNormalized == emailOrUsername || u.UsernameNormalized == emailOrUsername)
            .Select(u => new { u.Id, u.Email, u.IsEmailVerified })
            .FirstOrDefaultAsync(cancellationToken);
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
        var token = await tokenService.GenerateTokenAsync(user.Id, TokenPurpose.EmailVerification);

        // Send verification email
        emailBackgroundJobs.Enqueue(user.Email, "Verify Email",
            @$"
            <div>
                <p>Click the link below to verify your email:</p>
                <a href=""{webSetting.Value.SiteOrigin()}/verify-email?token={token.Value}"">Verify Email</a>
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
