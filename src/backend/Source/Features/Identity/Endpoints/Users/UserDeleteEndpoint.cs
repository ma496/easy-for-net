namespace Backend.Features.Identity.Endpoints.Users;

using Backend.Features.Identity.Core;

/// <summary>
/// This endpoint that handles <c>DELETE /users/{id}</c> to remove an existing user (refusing to delete a system-created user).
/// </summary>
[AllowPlatformNoTenant]
sealed class UserDeleteEndpoint(IUserService userService,
                                ICurrentUserService currentUserService,
                                ITenantAuthorizationService tenantAuthorizationService,
                                ITenantContext tenantContext) : Endpoint<UserDeleteRequest, UserDeleteResponse>
{
    /// <summary>
    /// The refusal reported when the account named is a shared identity: it belongs to another tenant
    /// as well, or holds a platform-scoped role. Deleting an account ends it everywhere, so a tenant
    /// may delete only an account that is its own.
    /// </summary>
    private const string SharedAccountMessage = "User belongs to other tenants and can only be deleted by a platform administrator";

    public override void Configure()
    {
        Delete("{id}");
        Group<UsersGroup>();
        Permissions(Allow.User_Delete);
    }

    public override async Task HandleAsync(UserDeleteRequest request, CancellationToken cancellationToken)
    {
        // The account is read from the set the caller may administer, so one holding no membership of
        // the tenant being acted in answers exactly as an account that does not exist does and is left
        // in place. A platform administrator administers every account.
        var entity = await userService.TenantUsers()
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        if (entity == null)
        {
            await Send.NotFoundAsync(cancellationToken);
            return;
        }
        if (entity.SystemCreated)
            ThrowError("System-created user cannot be deleted", ErrorCodes.SystemCreatedUserCannotBeDeleted);

        // An account is one identity across every tenant it belongs to, and deleting it ends it in all
        // of them. Administering one tenant is therefore not standing enough to delete an account that
        // also belongs to another tenant or holds a platform-scoped role: that account is deleted by a
        // platform administrator, while this tenant removes it from its own membership instead.
        if (!currentUserService.HasPermission(Allow.Platform_Administration))
        {
            if (tenantContext.CurrentTenantId is not { } activeTenantId
                || await tenantAuthorizationService.ReachesBeyondTenantAsync(entity.Id, activeTenantId, cancellationToken))
            {
                ThrowError(SharedAccountMessage, ErrorCodes.UserSharedAcrossTenants);
            }
        }

        // Delete the entity from the db - the account already read above, so the deletion cannot reach
        // one the caller may not administer.
        await userService.DeleteAsync(entity);
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


