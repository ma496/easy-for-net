namespace Backend.Features.Identity.Endpoints.Users;

using Backend.Features.Identity.Core;
using Backend.Features.Identity.Core.Entities;

/// <summary>
/// This endpoint that handles <c>PUT /users/{id}</c> to update a user's profile, active state, and role memberships.
/// </summary>
[AllowPlatformNoTenant]
sealed class UserUpdateEndpoint(IUserService userService, AppDbContext dbContext)
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
        // The account is read from the set the caller may administer, so one holding no membership of
        // the tenant being acted in answers exactly as an account that does not exist does, and nothing
        // below runs against it. A platform administrator administers every account.
        var entity = await userService.TenantUsers()
            .Include(x => x.UserRoles)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        if (entity == null)
        {
            await Send.NotFoundAsync(cancellationToken);
            return;
        }
        if (entity.SystemCreated)
            ThrowError("System-created user cannot be updated", ErrorCodes.SystemCreatedUserCannotBeUpdated);

        // The roles this endpoint may hand out and take away are the roles of the tenant being acted in
        // and no others, so they are read straight through the tenant query filter rather than through
        // IRoleService.Roles(), which widens for a platform administrator: were the two sets read from
        // different authorities, a role could be assignable and yet impossible to unassign, and the
        // response would report a removal that never happened. One query answers both halves, over the
        // roles named by the request and the roles the account already holds.
        var requestedRoleIds = request.Roles.Distinct().ToList();
        var assignedRoleIds = entity.UserRoles.Select(userRole => userRole.RoleId).ToList();
        var tenantRoleIds = await dbContext.Roles
            .AsNoTracking()
            .Where(role => requestedRoleIds.Contains(role.Id) || assignedRoleIds.Contains(role.Id))
            .Select(role => role.Id)
            .ToListAsync(cancellationToken);

        // A role belonging to another tenant is not found here, exactly as a role that does not exist
        // is not, and both are refused the same way.
        if (requestedRoleIds.Any(roleId => !tenantRoleIds.Contains(roleId)))
        {
            ThrowError(x => x.Roles, "Referenced record does not exist.", ErrorCodes.ReferencedRecordNotFound);
        }

        var requestMapper = new UserUpdateRequestMapper();
        requestMapper.Update(request, entity);
        // update user roles based on request and already assigned roles
        var rolesToAssign = requestedRoleIds.Where(x => entity.UserRoles.All(ur => ur.RoleId != x)).ToList();
        foreach (var role in rolesToAssign)
        {
            entity.UserRoles.Add(new UserRole { RoleId = role });
        }
        // An assignment to a role of another tenant is left alone: administering an account from inside
        // one tenant never strips what it holds in another.
        var rolesToRemove = entity.UserRoles
            .Where(x => tenantRoleIds.Contains(x.RoleId) && !requestedRoleIds.Contains(x.RoleId))
            .ToList();
        foreach (var role in rolesToRemove)
        {
            entity.UserRoles.Remove(role);
        }

        // save entity to db
        await userService.UpdateAsync(entity);
        var responseMapper = new UserUpdateResponseMapper();
        var response = responseMapper.Map(entity);
        // The roles echoed back are the tenant's own - the set just written - rather than every
        // assignment the account holds, so the response never names another tenant's role.
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
/// role ids are filled in by the endpoint rather than mapped, because only the roles the caller may see
/// are echoed back.
/// </summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class UserUpdateResponseMapper
{
    [MapperIgnoreTarget(nameof(UserUpdateResponse.Roles))]
    public partial UserUpdateResponse Map(User entity);
}


