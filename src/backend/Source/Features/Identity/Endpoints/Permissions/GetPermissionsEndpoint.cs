namespace Backend.Features.Identity.Endpoints.Permissions;

using Backend.Features.Identity.Core;

/// <summary>
/// GET endpoint that returns the list of permissions currently stored in the database,
/// used by administrative UIs to manage role/permission assignments.
/// </summary>
sealed class GetPermissionsEndpoint(IPermissionService permissionService) : EndpointWithoutRequest<GetPermissionsResponse>
{
    public override void Configure()
    {
        Get("get");
        Group<PermissionsGroup>();
    }

    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var permissions = await permissionService
            .Permissions()
            .Select(p => new PermissionDto
            {
                Id = p.Id,
                Name = p.Name,
                DisplayName = p.DisplayName
            })
            .ToListAsync(cancellationToken);

        await Send.ResponseAsync(new()
        {
            Permissions = permissions
        }, cancellation: cancellationToken);
    }
}

/// <summary>
/// Response payload for the permissions listing endpoint, containing the currently
/// stored permissions.
/// </summary>
sealed class GetPermissionsResponse
{
    public IReadOnlyList<PermissionDto> Permissions { get; set; } = [];
}

/// <summary>
/// Lightweight DTO representing a permission's identity, code-friendly name, and
/// human-readable display name.
/// </summary>
sealed class PermissionDto : BaseDto<Guid>
{
    public string Name { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
}


