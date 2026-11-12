using Backend.Features.Identity.Core;
using Backend.Features.Identity.Core.Sessions;

namespace Backend.Features.Identity.Endpoints.Roles;

/// <summary>
/// This endpoint that handles <c>DELETE /roles/{id}</c> to remove an existing role (refusing to delete a system-created role).
/// </summary>
/// <remarks>
/// A system-created role is refused outright. Deletion is soft: the row is retained and stops being read
/// anywhere, so the role grants nothing from the next request on while its name stays reserved and
/// cannot be taken by a role created afterwards. Once the deletion is saved, the sessions of every
/// account that held the role end, since each carries the permissions the role granted when it was minted.
/// </remarks>
sealed class RoleDeleteEndpoint(IRoleService roleService,
                                AppDbContext dbContext,
                                ISessionRevocationService sessionRevocationService) : Endpoint<RoleDeleteRequest, RoleDeleteResponse>
{
    public override void Configure()
    {
        Delete("{id}");
        Group<RolesGroup>();
        Permissions(Allow.Role_Delete);
    }

    public override async Task HandleAsync(RoleDeleteRequest request, CancellationToken cancellationToken)
    {
        var entity = await roleService.GetByIdAsync(request.Id);
        if (entity == null)
        {
            await Send.NotFoundAsync(cancellationToken);
            return;
        }
        if (entity.SystemCreated)
            this.ThrowError(ErrorCodes.SystemCreatedRoleCannotBeDeleted);

        // Delete the entity from the db - a soft delete, so the row survives to go on reserving the
        // role's name under the uniqueness constraint.
        var holderIds = await dbContext.UserRoles
            .AsNoTracking()
            .Where(userRole => userRole.RoleId == entity.Id)
            .Select(userRole => userRole.UserId)
            .ToListAsync(cancellationToken);
        await roleService.DeleteAsync(entity);
        await sessionRevocationService.RevokeUsersAsync(holderIds, cancellationToken);
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