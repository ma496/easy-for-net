namespace Backend.Features.Identity.Endpoints.Account;

using Backend.Features.Identity.Core;

/// <summary>
/// Authenticated POST endpoint that changes the current user's password after verifying the
/// existing one.
/// </summary>
sealed class ChangePasswordEndpoint(AppDbContext dbContext,
                                    ICurrentUserService currentUserService,
                                    IUserService userService,
                                    IAuthTokenService authTokenService)
    : Endpoint<ChangePasswordRequest, EmptyResponse>
{
    public override void Configure()
    {
        Post("change-password");
        Group<AccountGroup>();
    }

    public override async Task HandleAsync(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetCurrentUserId();
        var user = await dbContext.Users.FindAsync([userId], cancellationToken);
        if (user is null)
        {
            await Send.NotFoundAsync(cancellationToken);
            return;
        }
        var verified = await userService.ValidatePasswordAsync(user, request.CurrentPassword);
        if (!verified)
        {
            ThrowError(x => x.CurrentPassword, "Current password is invalid", ErrorCodes.InvalidCurrentPassword);
            return;
        }
        await userService.UpdatePasswordAsync(user, request.NewPassword);
        await authTokenService.RevokeAllAsync(user.Id, cancellationToken);
        await Send.OkAsync(cancellationToken);
    }
}

/// <summary>
/// Request payload for changing a password, containing the current and new password values.
/// </summary>
sealed class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = null!;
    public string NewPassword { get; set; } = null!;
}

/// <summary>
/// FluentValidation rules for <see cref="ChangePasswordRequest"/>, enforcing presence of
/// the current password and a minimum/maximum length for the new password.
/// </summary>
sealed class ChangePasswordValidator : Validator<ChangePasswordRequest>
{
    public ChangePasswordValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8).MaximumLength(50);
    }
}

