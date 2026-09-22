namespace Backend.Features.Identity.Endpoints.Permissions;

using Backend.Features.Identity.Core;
using Backend.Features.Tenancy.Core;

/// <summary>
/// GET endpoint that returns the set of permission groups defined in code via the
/// permission-definition service (used to drive role/permission editors), narrowed to the scope the
/// caller is acting in.
/// </summary>
/// <remarks>
/// The scope decides what is offered: a platform account acting in no tenant sees the platform tier
/// and the permissions declared for both scopes, and every other caller - including a platform account
/// that has entered a tenant - sees the tenant tier and those same both-scope permissions. So the
/// role-permission surface never offers a permission the caller could not exercise where they are, nor
/// one that could not be granted through the role being edited. Each definition carries its
/// <see cref="PermissionScope"/>, so the tiers stay distinguishable in the response.
/// <para>
/// The tenant's plan narrows it a second time: a permission whose feature is switched off is not
/// offered here, because a session acting in that tenant would not be minted with it either. The two
/// narrowings are stated in one place apiece and neither restates the other.
/// </para>
/// </remarks>
sealed class GetDefinePermissionsEndpoint(
    IPermissionDefinitionService permissionDefinitionService,
    IPermissionFeatureFilter permissionFeatureFilter,
    ICurrentUserService currentUserService,
    ITenantContext tenantContext) : EndpointWithoutRequest<GetDefinePermissionsResponse>
{
    public override void Configure()
    {
        Get("define");
        Group<PermissionsGroup>();
    }

    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var viewScope = currentUserService.IsPlatform() && tenantContext.IsPlatformScope()
            ? PermissionScope.Platform
            : PermissionScope.Tenant;
        var groups = permissionDefinitionService.GetPermissionGroups(viewScope);
        groups = await permissionFeatureFilter.FilterGroupsAsync(
            groups,
            new FeatureTarget(tenantContext.IsResolved ? tenantContext.CurrentTenantId : null),
            cancellationToken);

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


