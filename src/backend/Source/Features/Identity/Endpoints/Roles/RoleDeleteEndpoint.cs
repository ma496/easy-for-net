namespace Backend.Features.Identity.Endpoints.Roles;

using Backend.Features.Identity.Core;

/// <summary>
/// This endpoint that handles <c>DELETE /roles/{id}</c> to remove an existing role (refusing to delete a system-created role).
/// </summary>
/// <remarks>
/// Only the roles of the tenant being acted in can be deleted here: the lookup answers for that tenant
/// alone, so a role belonging to another tenant is reported missing and is left untouched, exactly as
/// an identifier naming no role at all would be. A platform account that has entered the tenant is the one
/// exception and reaches any tenant's role. A tenant's system-created administrator role is refused
/// outright, so a tenant cannot be left without one. Deletion is soft: the row is retained and stops
/// being read anywhere, so the role grants nothing from the next request on while its name stays
/// reserved within its tenant and cannot be taken by a role created afterwards.
/// </remarks>
[AllowPlatformNoTenant]
sealed class RoleDeleteEndpoint(IRoleService roleService) : Endpoint<RoleDeleteRequest, RoleDeleteResponse>
{
    private const string SystemCreatedMessage = "System-created role cannot be deleted";

    public override void Configure()
    {
        Delete("{id}");
        Group<RolesGroup>();
        Permissions(Allow.Role_Delete);
    }

    public override async Task HandleAsync(RoleDeleteRequest request, CancellationToken cancellationToken)
    {
        // get entity from db - narrowed to the roles the caller may see, so the tenant restriction and
        // the missing-row case are the same code path and produce the same 404.
        var entity = await roleService.GetByIdAsync(request.Id);
        if (entity == null)
        {
            await Send.NotFoundAsync(cancellationToken);
            return;
        }
        if (entity.SystemCreated)
            ThrowError(SystemCreatedMessage, ErrorCodes.SystemCreatedRoleCannotBeDeleted);

        // Delete the entity from the db - a soft delete, so the row survives to go on reserving the
        // role's name under the tenant's uniqueness constraint.
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