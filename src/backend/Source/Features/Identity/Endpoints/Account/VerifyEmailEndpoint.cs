namespace Backend.Features.Identity.Endpoints.Account;

using Backend.Features.Identity.Core;
using Backend.Features.Identity.Core.Entities;

/// <summary>
/// Anonymous POST endpoint that marks a user's email as verified after validating the
/// verification token delivered to their inbox.
/// </summary>
sealed class VerifyEmailEndpoint(ITokenService tokenService, IUserService userService, AppDbContext dbContext)
    : Endpoint<VerifyEmailRequest, EmptyResponse>
{
    public override void Configure()
    {
        Post("verify-email");
        Group<AccountGroup>();
        AllowAnonymous();
    }

    public override async Task HandleAsync(VerifyEmailRequest request, CancellationToken cancellationToken)
    {
        var token = await tokenService.GetTokenAsync(request.Token, TokenPurpose.EmailVerification, cancellationToken);
        if (token == null || !tokenService.ValidateToken(token))
        {
            ThrowError("Invalid or expired token", ErrorCodes.InvalidToken);
        }

        var user = await userService.GetByIdAsync(token.UserId);
        if (user == null)
        {
            ThrowError("User not found", ErrorCodes.UserNotFound);
            return;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        user.IsEmailVerified = true;
        await userService.UpdateAsync(user);
        if (!await tokenService.UseTokenAsync(token, cancellationToken))
        {
            ThrowError("Invalid or expired token", ErrorCodes.InvalidToken);
        }

        await transaction.CommitAsync(cancellationToken);

        await Send.OkAsync(cancellationToken);
    }
}

/// <summary>
/// Request payload for the email verification endpoint, carrying the verification token
/// delivered to the user's email address.
/// </summary>
sealed class VerifyEmailRequest
{
    public string Token { get; set; } = null!;
}

/// <summary>
/// FluentValidation rules for <see cref="VerifyEmailRequest"/>, requiring a non-empty
/// verification token.
/// </summary>
sealed class VerifyEmailValidator : Validator<VerifyEmailRequest>
{
    public VerifyEmailValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty();
    }
}
