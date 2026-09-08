namespace Backend.Features.Identity.Endpoints.Users;

using Backend.Features.Identity.Core;

/// <summary>
/// This endpoint that handles <c>DELETE /users/{id}</c> to remove an existing user (refusing to delete a system-created user).
/// </summary>
sealed class UserDeleteEndpoint(IUserService userService) : Endpoint<UserDeleteRequest, UserDeleteResponse>
{
    public override void Configure()
    {
        Delete("{id}");
        Group<UsersGroup>();
        Permissions(Allow.User_Delete);
    }

    public override async Task HandleAsync(UserDeleteRequest request, CancellationToken cancellationToken)
    {
        // get entity from db
        var entity = await userService.GetByIdAsync(request.Id);
        if (entity == null)
        {
            await Send.NotFoundAsync(cancellationToken);
            return;
        }
        if (entity.SystemCreated)
            ThrowError("System-created user cannot be deleted", ErrorCodes.SystemCreatedUserCannotBeDeleted);

        // Delete the entity from the db
        await userService.DeleteAsync(request.Id);
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


