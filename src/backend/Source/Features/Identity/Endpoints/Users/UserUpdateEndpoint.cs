namespace Backend.Features.Identity.Endpoints.Users;

using Backend.Features.Identity.Core;
using Backend.Features.Identity.Core.Entities;

/// <summary>
/// This endpoint that handles <c>PUT /users/{id}</c> to update a user's profile, active state, and role memberships.
/// </summary>
sealed class UserUpdateEndpoint(IUserService userService,
                                AppDbContext dbContext,
                                ICurrentUserService currentUserService,
                                ITenantAuthorizationService tenantAuthorizationService,
                                ITenantContext tenantContext)
    : Endpoint<UserUpdateRequest, UserUpdateResponse>
{
    /// <summary>
    /// The refusal reported when the account named is a shared identity: it belongs to another tenant
    /// as well, or holds a platform-scoped role. What this endpoint writes - the names, and whether the
    /// account is active at all - follows the account into every tenant it belongs to, so a tenant may
    /// write it only for an account that is its own.
    /// </summary>
    private const string SharedAccountMessage = "User belongs to other tenants and can only be updated by a platform administrator";

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

        // An account is one identity across every tenant it belongs to, and the fields written below are
        // the account's own rather than this tenant's view of it: deactivating it locks it out
        // everywhere, and renaming it renames it everywhere. Administering one tenant is therefore not
        // standing enough to write them for an account that also belongs to another tenant or holds a
        // platform-scoped role - otherwise anyone who can create a tenant could add a member of somebody
        // else's tenant to it and lock them out of theirs. Such an account is administered by its owner
        // or by a platform administrator; what it may do *inside this tenant* stays administrable here,
        // through the tenant's own membership surface.
        await GuardSharedAccountAsync(entity.Id, cancellationToken);

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

    /// <summary>
    /// Refuses the request when the account reaches beyond the tenant being acted in. A platform
    /// account acting in no tenant administers the platform's own accounts and is never refused here;
    /// inside a tenant it is an actor of that tenant and is refused exactly as its administrators are.
    /// </summary>
    /// <param name="userId">The account being written.</param>
    /// <param name="cancellationToken">Token used to cancel the read.</param>
    private async Task GuardSharedAccountAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (currentUserService.IsPlatform() && tenantContext.IsPlatformScope())
        {
            return;
        }

        // A caller acting in no tenant who is not a platform account administers no accounts at all -
        // the set this account was read from is empty for them - so reaching this with no tenant means
        // something above changed; it is refused rather than waved through.
        if (tenantContext.CurrentTenantId is not { } activeTenantId
            || await tenantAuthorizationService.ReachesBeyondTenantAsync(userId, activeTenantId, cancellationToken))
        {
            ThrowError(SharedAccountMessage, ErrorCodes.UserSharedAcrossTenants);
        }
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


