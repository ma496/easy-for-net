namespace Backend.Features.Identity.Endpoints.Roles;

using Backend.Features.Identity.Core;

/// <summary>
/// This endpoint that handles <c>DELETE /roles/{id}</c> to remove an existing role (refusing to delete a system-created role).
/// </summary>
sealed class RoleDeleteEndpoint(IRoleService roleService) : Endpoint<RoleDeleteRequest, RoleDeleteResponse>
{
    public override void Configure()
    {
        Delete("{id}");
        Group<RolesGroup>();
        Permissions(Allow.Role_Delete);
    }

    public override async Task HandleAsync(RoleDeleteRequest request, CancellationToken cancellationToken)
    {
        // get entity from db
        var entity = await roleService.GetByIdAsync(request.Id);
        if (entity == null)
        {
            await Send.NotFoundAsync(cancellationToken);
            return;
        }
        if (entity.SystemCreated)
            ThrowError("System-created role cannot be deleted", ErrorCodes.SystemCreatedRoleCannotBeDeleted);

        // Delete the entity from the db
        await roleService.DeleteAsync(entity);
        await Send.ResponseAsync(new() { Success = true }, cancellation: cancellationToken);
    }
}

/// <summary>
/// Request payload identifying the role to delete by id.
/// </summary>
sealed class RoleDeleteRequest : BaseDto<Guid>
{
}

/// <summary>
/// FluentValidation rules requiring a non-empty id for role deletion.
/// </summary>
sealed class RoleDeleteValidator : Validator<RoleDeleteRequest>
{
    public RoleDeleteValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

/// <summary>
/// Response payload indicating the outcome of a role deletion attempt.
/// </summary>
sealed class RoleDeleteResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = null!;
}


