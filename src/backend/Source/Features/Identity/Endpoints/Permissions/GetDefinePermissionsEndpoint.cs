namespace Backend.Features.Identity.Endpoints.Permissions;

using Backend.Features.Identity.Core;

/// <summary>
/// GET endpoint that returns the set of permission groups defined in code via the
/// permission-definition service (used to drive role/permission editors), narrowed to the tier the
/// caller is able to grant.
/// </summary>
/// <remarks>
/// A caller without <see cref="Allow.Platform_Administration"/> only ever sees tenant-tier
/// definitions, so the role-permission surface never offers a permission that could not be granted
/// through a tenant role. A platform administrator receives the whole catalogue, with every
/// platform-tier definition carrying <see cref="PermissionDefinition.IsPlatform"/> so the two tiers
/// stay distinguishable.
/// </remarks>
sealed class GetDefinePermissionsEndpoint(
    IPermissionDefinitionService permissionDefinitionService,
    ICurrentUserService currentUserService) : EndpointWithoutRequest<GetDefinePermissionsResponse>
{
    public override void Configure()
    {
        Get("define");
        Group<PermissionsGroup>();
    }

    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var includePlatformPermissions = currentUserService.HasPermission(Allow.Platform_Administration);
        var groups = permissionDefinitionService.GetPermissionGroups(includePlatformPermissions);

        await Send.ResponseAsync(new()
        {
            Groups = groups
        }, cancellation: cancellationToken);
    }
}

/// <summary>
/// Response payload for the "define" permissions endpoint, containing the
/// permission groups the caller is allowed to see.
/// </summary>
sealed class GetDefinePermissionsResponse
{
    public IReadOnlyList<PermissionGroupDefinition> Groups { get; set; } = [];
}


