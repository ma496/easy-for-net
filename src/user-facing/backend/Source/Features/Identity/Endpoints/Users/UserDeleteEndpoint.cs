using Backend.Features.Identity.Core;
using Backend.Features.Identity.Core.Sessions;

namespace Backend.Features.Identity.Endpoints.Users;

/// <summary>
/// This endpoint that handles <c>DELETE /users/{id}</c> to remove an existing user (refusing to delete a system-created user).
/// </summary>
sealed class UserDeleteEndpoint(IUserService userService,
                                ISessionRevocationService sessionRevocationService) : Endpoint<UserDeleteRequest, UserDeleteResponse>
{
    public override void Configure()
    {
        Delete("{id}");
        Group<UsersGroup>();
        Permissions(Allow.User_Delete);
    }

    public override async Task HandleAsync(UserDeleteRequest request, CancellationToken cancellationToken)
    {
        var entity = await userService.Users()
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        if (entity == null)
        {
            await Send.NotFoundAsync(cancellationToken);
            return;
        }
        if (entity.SystemCreated)
            this.ThrowError(ErrorCodes.SystemCreatedUserCannotBeDeleted);

        await userService.DeleteAsync(entity);

        // A deleted account keeps no session anywhere, ended once the deletion has been saved.
        await sessionRevocationService.RevokeUserAsync(entity.Id, cancellationToken);
        await Send.ResponseAsync(new() { Success = true }, cancellation: cancellationToken);
    }
}

/// <summary>
/// Request payload identifying the user to delete by id.
/// </summary>
sealed class UserDeleteRequest : BaseDto<Guid>
{
}

/// <summary>
/// FluentValidation rules requiring a non-empty id for user deletion.
/// </summary>
sealed class UserDeleteValidator : Validator<UserDeleteRequest>
{
    public UserDeleteValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

/// <summary>
/// Response payload indicating the outcome of a user deletion attempt.
/// </summary>
sealed class UserDeleteResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = null!;
}
