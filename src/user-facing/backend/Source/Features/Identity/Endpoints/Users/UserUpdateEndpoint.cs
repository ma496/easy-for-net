using Backend.Features.Identity.Core;
using Backend.Features.Identity.Core.Entities;
using Backend.Features.Identity.Core.Sessions;

namespace Backend.Features.Identity.Endpoints.Users;

/// <summary>
/// This endpoint that handles <c>PUT /users/{id}</c> to update a user's profile, active state, and role memberships.
/// </summary>
/// <remarks>
/// A session carries the grants it was minted with, so once the change has committed the sessions it
/// invalidates are ended: deactivating the account ends every session it holds, and changing its roles
/// ends its sessions too.
/// </remarks>
sealed class UserUpdateEndpoint(IUserService userService,
                                AppDbContext dbContext,
                                ISessionRevocationService sessionRevocationService)
    : Endpoint<UserUpdateRequest, UserUpdateResponse>
{
    public override void Configure()
    {
        Put("{id}");
        Group<UsersGroup>();
        Permissions(Allow.User_Update);
    }

    public override async Task HandleAsync(UserUpdateRequest request, CancellationToken cancellationToken)
    {
        var entity = await userService.Users()
            .Include(x => x.UserRoles)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        if (entity == null)
        {
            await Send.NotFoundAsync(cancellationToken);
            return;
        }
        if (entity.SystemCreated)
            this.ThrowError(ErrorCodes.SystemCreatedUserCannotBeUpdated);

        // One query answers both halves: the roles named by the request and the roles the account
        // already holds, as long as they exist. An assignment to a deleted role is left alone.
        var requestedRoleIds = request.Roles.Distinct().ToList();
        var assignedRoleIds = entity.UserRoles.Select(userRole => userRole.RoleId).ToList();
        var existingRoleIds = await dbContext.Roles
            .AsNoTracking()
            .Where(role => requestedRoleIds.Contains(role.Id) || assignedRoleIds.Contains(role.Id))
            .Select(role => role.Id)
            .ToListAsync(cancellationToken);

        if (requestedRoleIds.Any(roleId => !existingRoleIds.Contains(roleId)))
        {
            this.ThrowError(x => x.Roles, ErrorCodes.ReferencedRecordNotFound);
        }

        var requestMapper = new UserUpdateRequestMapper();
        requestMapper.Update(request, entity);
        // update user roles based on request and already assigned roles
        var rolesToAssign = requestedRoleIds.Where(x => entity.UserRoles.All(ur => ur.RoleId != x)).ToList();
        foreach (var role in rolesToAssign)
        {
            entity.UserRoles.Add(new UserRole { RoleId = role });
        }
        var rolesToRemove = entity.UserRoles
            .Where(x => existingRoleIds.Contains(x.RoleId) && !requestedRoleIds.Contains(x.RoleId))
            .ToList();
        foreach (var role in rolesToRemove)
        {
            entity.UserRoles.Remove(role);
        }

        // A new stamp on deactivation, so a refresh racing the revocation below is withdrawn by the stamp re-check in TokenService.
        if (!entity.IsActive)
        {
            entity.SecurityStamp = Guid.NewGuid();
        }

        // save entity to db
        await userService.UpdateAsync(entity);

        // Only after the change is saved, so a refused or failed update revokes nothing.
        if (!entity.IsActive)
        {
            await sessionRevocationService.RevokeUserAsync(entity.Id, cancellationToken);
        }
        else if (rolesToAssign.Count > 0 || rolesToRemove.Count > 0)
        {
            await sessionRevocationService.RevokeUserAsync(entity.Id, cancellationToken);
        }

        var responseMapper = new UserUpdateResponseMapper();
        var response = responseMapper.Map(entity);
        response.Roles = requestedRoleIds;
        await Send.ResponseAsync(response, cancellation: cancellationToken);
    }

}

/// <summary>
/// Request payload for updating an existing user, including the new set of role assignments.
/// </summary>
public sealed class UserUpdateRequest : BaseDto<Guid>
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public bool IsActive { get; set; }
    public List<Guid> Roles { get; set; } = [];
}

/// <summary>
/// FluentValidation rules for an update-user request, requiring at least one role and length-bounding optional name fields.
/// </summary>
sealed class UserUpdateValidator : Validator<UserUpdateRequest>
{
    public UserUpdateValidator()
    {
        RuleFor(x => x.FirstName).MinimumLength(3).MaximumLength(50).When(x => !x.FirstName.IsNullOrEmpty());
        RuleFor(x => x.LastName).MinimumLength(3).MaximumLength(50).When(x => !x.LastName.IsNullOrEmpty());
        RuleFor(x => x.Roles).NotEmpty();
    }
}

/// <summary>
/// Response payload returned after a successful user update, echoing the user's id and updated fields.
/// </summary>
public sealed class UserUpdateResponse : BaseDto<Guid>
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public bool IsActive { get; set; }
    public List<Guid> Roles { get; set; } = [];
}

/// <summary>
/// This mapper that updates a <see cref="User"/> entity in-place from a <see cref="UserUpdateRequest"/>, ignoring the role list which is handled separately.
/// </summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Source)]
public partial class UserUpdateRequestMapper
{
    [MapperIgnoreSource(nameof(UserUpdateRequest.Roles))]
    public partial void Update(UserUpdateRequest request, User entity);
}

/// <summary>
/// This mapper that projects a <see cref="User"/> entity into a <see cref="UserUpdateResponse"/>. The
/// role ids are filled in by the endpoint rather than mapped: the set just written is echoed back.
/// </summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class UserUpdateResponseMapper
{
    [MapperIgnoreTarget(nameof(UserUpdateResponse.Roles))]
    public partial UserUpdateResponse Map(User entity);
}
