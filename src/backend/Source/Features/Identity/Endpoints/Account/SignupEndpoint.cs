namespace Backend.Features.Identity.Endpoints.Account;

using Backend.External.Email;
using Backend.Features.Identity.Core;
using Backend.Features.Identity.Core.Entities;
using Backend.Settings;
using Microsoft.Extensions.Options;

/// <summary>
/// Anonymous POST endpoint that registers a new user, assigns the default <c>Public</c> role,
/// and optionally triggers an email verification workflow.
/// </summary>
sealed class SignupEndpoint(IUserService userService,
                            ITokenService tokenService,
                            IEmailBackgroundJobs emailBackgroundJobs,
                            IOptions<WebSetting> webSetting,
                            IOptions<SigninSetting> signinSetting,
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
        var usernameExists = await dbContext.Users
            .AnyAsync(x => x.UsernameNormalized == request.Username.Trim().ToLowerInvariant(), cancellationToken);
        if (usernameExists)
        {
            ThrowError("Username already exists", ErrorCodes.UsernameAlreadyExists);
        }
        
        var emailExists = await dbContext.Users
            .AnyAsync(x => x.EmailNormalized == request.Email.Trim().ToLowerInvariant(), cancellationToken);
        if (emailExists)
        {
            ThrowError("Email already exists", ErrorCodes.EmailAlreadyExists);
        }

        using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var user = new User
        {
            Email = request.Email,
            Username = request.Username,
            IsActive = true,
            IsEmailVerified = false
        };

        await userService.CreateAsync(user, request.Password);

        var publicRole = await dbContext.Roles
            .Where(x => x.Name == "Public")
            .Select(x => new { x.Id })
            .FirstOrDefaultAsync(cancellationToken);
        if (publicRole == null)
        {
            throw new Exception("Public role not found");
        }
        await userService.AssignRoleAsync(user.Id, publicRole.Id);

        if (signinSetting.Value.IsEmailVerificationRequired)
        {
            // Generate verification token
            var token = await tokenService.GenerateTokenAsync(user, TokenPurpose.EmailVerification);

            // Send verification email
            emailBackgroundJobs.Enqueue(user.Email, "Verify Email",
                @$"
            <div>
                <p>Click the link below to verify your email:</p>
                <a href=""{webSetting.Value.DefaultDomain}/verify-email?token={token.Value}"">Verify Email</a>
            </div>", true);

            await transaction.CommitAsync(cancellationToken);

            await Send.OkAsync(new SignupResponse { IsEmailVerificationRequired = true }, cancellationToken);
        }
        else
        {
            await transaction.CommitAsync(cancellationToken);

            await Send.OkAsync(new SignupResponse { IsEmailVerificationRequired = false }, cancellationToken);
        }
    }
}

/// <summary>
/// Request payload for creating a new account, including the desired username, email,
/// password, and a password confirmation field.
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
/// format, password complexity, and password/confirm-password matching.
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
/// Response returned after a successful signup, indicating whether the user must
/// verify their email before they can sign in.
/// </summary>
sealed class SignupResponse
{
    public bool IsEmailVerificationRequired { get; set; }
}
