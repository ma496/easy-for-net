namespace Backend.Features.Identity.Endpoints.Account;

using Backend.Features.Identity.Core;
using Backend.Features.Identity.Core.Entities;

/// <summary>
/// Anonymous POST endpoint that completes the password-reset flow by validating a
/// previously issued reset token and updating the user's password.
/// </summary>
sealed class ResetPasswordEndpoint(ITokenService tokenService,
                                   IUserService userService,
                                   IPasswordHasher passwordHasher,
                                   IAuthTokenService authTokenService,
                                   AppDbContext dbContext)
    : Endpoint<ResetPasswordRequest>
{
    public override void Configure()
    {
        Post("reset-password");
        Group<AccountGroup>();
        AllowAnonymous();
    }

    public override async Task HandleAsync(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var token = await tokenService.GetTokenAsync(request.Token, TokenPurpose.PasswordReset, cancellationToken);
        if (token == null)
        {
            ThrowError("Token is invalid", ErrorCodes.InvalidToken);
        }
        if (!tokenService.ValidateToken(token))
        {
            ThrowError("Token is expired", ErrorCodes.TokenExpired);
        }
        var user = await userService.GetByIdAsync(token.UserId);
        if (user == null)
        {
            await Send.NotFoundAsync(cancellationToken);
            return;
        }
        user.PasswordHash = passwordHasher.HashPassword(request.Password);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await userService.UpdateAsync(user);
        if (!await tokenService.UseTokenAsync(token, cancellationToken))
        {
            ThrowError("Token is invalid", ErrorCodes.InvalidToken);
        }
        await authTokenService.RevokeAllAsync(user.Id, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await Send.OkAsync(cancellation: cancellationToken);
    }
}

/// <summary>
/// Request payload for resetting a password, supplying the reset token together with
/// the new password value.
/// </summary>
sealed class ResetPasswordRequest
{
    public string Token { get; set; } = null!;
    public string Password { get; set; } = null!;
}

/// <summary>
/// FluentValidation rules for <see cref="ResetPasswordRequest"/>, ensuring a valid, 
/// non-empty token and password.
/// </summary>
sealed class ResetPasswordRequestValidator : Validator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty();

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8)
            .MaximumLength(50);
    }
}
