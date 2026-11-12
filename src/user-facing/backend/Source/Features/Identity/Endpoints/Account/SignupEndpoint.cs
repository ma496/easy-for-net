using Backend.External.Email;
using Backend.Features.Identity.Core;
using Backend.Features.Identity.Core.Entities;
using Backend.Features.Settings.Core;
using Backend.Settings;

namespace Backend.Features.Identity.Endpoints.Account;

/// <summary>
/// Anonymous POST endpoint that registers a new account and optionally triggers an email
/// verification workflow.
/// </summary>
/// <remarks>
/// Sign-up is open only while <c>General.IsSignupEnabled</c> is on - on by default - and refused
/// otherwise with 403 <c>signupDisabled</c>, before anything is looked up or written. The account is
/// given the role <c>General.SignupRoleName</c> names, when a role of that name exists. The
/// verification email links to the site origin, <c>Web:DefaultDomain</c>.
/// </remarks>
sealed class SignupEndpoint(IUserService userService,
                            ITokenService tokenService,
                            IEmailBackgroundJobs emailBackgroundJobs,
                            IOptions<WebSetting> webSetting,
                            ISettingProvider settingProvider,
                            AppDbContext dbContext)
    : Endpoint<SignupRequest, SignupResponse>
{
    public override void Configure()
    {
        Post("signup");
        Group<AccountGroup>();
        AllowAnonymous();
    }

    public override async Task HandleAsync(SignupRequest request, CancellationToken cancellationToken)
    {
        var generalSettings = await settingProvider.GetAsync<GeneralSettings>(cancellationToken);
        if (!generalSettings.IsSignupEnabled)
        {
            this.ThrowError(ErrorCodes.SignupDisabled, StatusCodes.Status403Forbidden);
        }

        var usernameExists = await dbContext.Users
            .AsNoTracking()
            .AnyAsync(x => x.UsernameNormalized == request.Username.Trim().ToLowerInvariant(), cancellationToken);
        if (usernameExists)
        {
            this.ThrowError(x => x.Username, ErrorCodes.UsernameAlreadyExists);
        }

        var emailExists = await dbContext.Users
            .AsNoTracking()
            .AnyAsync(x => x.EmailNormalized == request.Email.Trim().ToLowerInvariant(), cancellationToken);
        if (emailExists)
        {
            this.ThrowError(x => x.Email, ErrorCodes.EmailAlreadyExists);
        }

        var signupRoleName = generalSettings.SignupRoleName.Trim().ToLowerInvariant();
        var signupRoleId = signupRoleName.Length == 0
            ? null
            : await dbContext.Roles
                .AsNoTracking()
                .Where(role => role.NameNormalized == signupRoleName)
                .Select(role => (Guid?)role.Id)
                .FirstOrDefaultAsync(cancellationToken);

        // One transaction over the account, its role and its verification token, so a failure part-way
        // leaves no account behind that cannot sign in.
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var user = new User
        {
            Email = request.Email,
            Username = request.Username,
            IsActive = true,
            IsEmailVerified = false
        };
        if (signupRoleId is { } roleId)
        {
            user.UserRoles.Add(new UserRole { RoleId = roleId });
        }

        await userService.CreateAsync(user, request.Password);

        var verificationToken = generalSettings.IsEmailVerificationRequired
            ? await tokenService.GenerateTokenAsync(user.Id, TokenPurpose.EmailVerification)
            : null;

        await transaction.CommitAsync(cancellationToken);

        // Queued only once everything is committed. The background store is written on its own
        // connection, so enqueuing before the commit would leave a verification email in flight
        // naming a token row that a rollback had just discarded.
        if (verificationToken is not null)
        {
            emailBackgroundJobs.Enqueue(user.Email, "Verify Email",
                @$"
            <div>
                <p>Click the link below to verify your email:</p>
                <a href=""{webSetting.Value.SiteOrigin()}/verify-email?token={verificationToken.Value}"">Verify Email</a>
            </div>", true);
        }

        await Send.OkAsync(new SignupResponse { IsEmailVerificationRequired = verificationToken is not null }, cancellationToken);
    }
}

/// <summary>
/// Request payload for creating a new account: the desired username, email, password and password
/// confirmation.
/// </summary>
sealed class SignupRequest
{
    public string Username { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string Password { get; set; } = null!;
    public string ConfirmPassword { get; set; } = null!;
}

/// <summary>
/// FluentValidation rules for <see cref="SignupRequest"/>, enforcing username/email
/// format, password length and password/confirm-password matching.
/// </summary>
sealed class SignupValidator : Validator<SignupRequest>
{
    public SignupValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty()
            .MinimumLength(3)
            .MaximumLength(50);

        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(100);

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8)
            .MaximumLength(50);

        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.Password)
            .WithMessage("Confirm password must match the password");
    }
}

/// <summary>
/// Response returned after a successful signup, indicating whether the user must verify their email
/// before they can sign in.
/// </summary>
sealed class SignupResponse
{
    public bool IsEmailVerificationRequired { get; set; }
}
