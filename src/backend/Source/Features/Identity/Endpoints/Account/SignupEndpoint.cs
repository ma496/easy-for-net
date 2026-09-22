namespace Backend.Features.Identity.Endpoints.Account;

using Backend.ShareData.Entities;
using Backend.External.Email;
using Backend.Features.Identity.Core;
using Backend.Features.Identity.Core.Entities;
using Backend.Features.Tenancy.Core;
using Backend.Settings;
using Microsoft.Extensions.Options;

/// <summary>
/// Anonymous POST endpoint that registers a new account together with the tenant it will work in,
/// and optionally triggers an email verification workflow. Signing up is how a tenant comes into
/// being without anybody's help: the account is created, the tenant is created, and the account is
/// made that tenant's first member and administrator, all as one act.
/// </summary>
/// <remarks>
/// The two are created together rather than one after the other because neither is useful without
/// the other here. An account belonging to no tenant can exercise no permission at all, so it could
/// not sign in; a tenant with no member is administrable by nobody. Creating them in one transaction
/// is what stops either from being left behind when the other fails.
/// <para>
/// The account created is an ordinary one, never a platform account: that tier belongs to the
/// platform's own administrators and no self-service surface can claim it.
/// </para>
/// </remarks>
sealed class SignupEndpoint(IUserService userService,
                            ITokenService tokenService,
                            ITenantService tenantService,
                            IEmailBackgroundJobs emailBackgroundJobs,
                            IOptions<WebSetting> webSetting,
                            IOptions<SigninSetting> signinSetting,
                            ITenantContext tenantContext,
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
            ThrowError(x => x.Username, "Username already exists", ErrorCodes.UsernameAlreadyExists);
        }

        var emailExists = await dbContext.Users
            .AnyAsync(x => x.EmailNormalized == request.Email.Trim().ToLowerInvariant(), cancellationToken);
        if (emailExists)
        {
            ThrowError(x => x.Email, "Email already exists", ErrorCodes.EmailAlreadyExists);
        }

        // Asked before anything is written, so a taken identifier is a field-level failure on this
        // request rather than a unique-index violation surfacing as a database error. The index is
        // still the backstop for two sign-ups racing for the same identifier.
        if (await tenantService.IdentifierExistsAsync(request.TenantIdentifier, cancellationToken: cancellationToken))
        {
            ThrowError(x => x.TenantIdentifier, ITenantService.DuplicateIdentifierMessage, ErrorCodes.TenantIdentifierAlreadyExists);
        }

        // Platform scope is established for the whole handler rather than for the account alone. Saving
        // applies the tenant rules across everything the context is tracking, so a scope left
        // unresolved - or left pointing at whichever tenant a signed-in caller happened to be acting in -
        // would decide where rows staged here land. This endpoint is anonymous and must not become a way
        // of adding a member to somebody else's tenant, so the account is created belonging to none, and
        // the membership that follows is written inside the new tenant's own scope by the tenant service.
        using (tenantContext.BeginPlatformScope())
        {
            // One transaction over both creations: an account without its tenant could not sign in, and
            // a tenant without its first member could be administered by nobody, so neither is allowed
            // to survive the other's failure. Disposed asynchronously, because the rollback this takes
            // on the way out of a failed sign-up - the duplicate-identifier backstop among them - is a
            // round trip that must not block the request thread.
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            var user = new User
            {
                Email = request.Email,
                Username = request.Username,
                IsActive = true,
                IsEmailVerified = false
            };

            // Saved before the tenant is created, because the tenant creation names this account as the
            // first member and the tenant service saves this same context as it writes.
            await userService.CreateAsync(user, request.Password);

            // The single creation path, shared with platform tenant creation: it provisions the tenant's
            // administrator role, gives this account an active membership, and grants it that role - so a
            // tenant created here is administrable from the moment it exists. It joins the transaction
            // opened above rather than opening one of its own.
            await tenantService.CreateAsync(
                new() { Name = request.TenantName, Identifier = request.TenantIdentifier },
                user.Id,
                cancellationToken);

            var verificationToken = signinSetting.Value.IsEmailVerificationRequired
                ? await tokenService.GenerateTokenAsync(user, TokenPurpose.EmailVerification)
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
                <a href=""{webSetting.Value.DefaultDomain}/verify-email?token={verificationToken.Value}"">Verify Email</a>
            </div>", true);
            }

            await Send.OkAsync(new SignupResponse { IsEmailVerificationRequired = verificationToken is not null }, cancellationToken);
        }
    }
}

/// <summary>
/// Request payload for creating a new account and the tenant it will work in: the desired username,
/// email, password and password confirmation, together with the tenant's display name and its
/// url-safe identifier.
/// </summary>
sealed class SignupRequest
{
    public string Username { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string Password { get; set; } = null!;
    public string ConfirmPassword { get; set; } = null!;

    /// <summary>
    /// Gets or sets the tenant's display name, as people will read it.
    /// </summary>
    public string TenantName { get; set; } = null!;

    /// <summary>
    /// Gets or sets the tenant's url-safe identifier: what somebody signing in to this tenant types
    /// when they belong to more than one, and what the tenant is addressed by rather than its key.
    /// </summary>
    public string TenantIdentifier { get; set; } = null!;
}

/// <summary>
/// FluentValidation rules for <see cref="SignupRequest"/>, enforcing username/email
/// format, password complexity, password/confirm-password matching, and the tenant naming rules.
/// </summary>
/// <remarks>
/// The tenant rules are the tenancy feature's own, chained here rather than restated, so the tenant
/// a sign-up creates is held to exactly what platform tenant creation and tenant update hold theirs
/// to and the three cannot drift apart.
/// </remarks>
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

        RuleFor(x => x.TenantName)
            .NotEmpty()
            .TenantName();

        RuleFor(x => x.TenantIdentifier)
            .NotEmpty()
            .TenantIdentifier();
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
