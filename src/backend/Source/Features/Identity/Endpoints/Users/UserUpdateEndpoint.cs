namespace Backend.Features.Identity.Endpoints.Users;

using Backend.Features.Identity.Core;
using Backend.Features.Identity.Core.Entities;

/// <summary>
/// This endpoint that handles <c>PUT /users/{id}</c> to update a user's profile, active state, and role memberships.
/// </summary>
sealed class UserUpdateEndpoint(IUserService userService)
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
        // get entity from db
        var entity = await userService.Users()
            .Include(x => x.UserRoles)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        if (entity == null)
        {
            await Send.NotFoundAsync(cancellationToken);
            return;
        }
        if (entity.SystemCreated)
            ThrowError("System-created user cannot be updated", ErrorCodes.SystemCreatedUserCannotBeUpdated);

        var requestMapper = new UserUpdateRequestMapper();
        requestMapper.Update(request, entity);
        // update user roles based on request and already assigned roles
        var rolesToAssign = request.Roles.Where(x => entity.UserRoles.All(ur => ur.RoleId != x)).ToList();
        foreach (var role in rolesToAssign)
        {
            entity.UserRoles.Add(new UserRole { RoleId = role });
        }
        var rolesToRemove = entity.UserRoles.Where(x => !request.Roles.Contains(x.RoleId)).ToList();
        foreach (var role in rolesToRemove)
        {
            entity.UserRoles.Remove(role);
        }

        // save entity to db
        await userService.UpdateAsync(entity);
        var responseMapper = new UserUpdateResponseMapper();
        await Send.ResponseAsync(responseMapper.Map(entity), cancellation: cancellationToken);
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
/// This mapper that projects a <see cref="User"/> entity into a <see cref="UserUpdateResponse"/>, collapsing <see cref="UserRole"/> join rows into role ids.
/// </summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class UserUpdateResponseMapper
{
    [MapProperty(nameof(User.UserRoles), nameof(UserUpdateResponse.Roles), Use = nameof(UserRolesToRoles))]
    public partial UserUpdateResponse Map(User entity);

    private static List<Guid> UserRolesToRoles(ICollection<UserRole> userRoles)
    {
        return [.. userRoles.Select(x => x.RoleId)];
    }
}


