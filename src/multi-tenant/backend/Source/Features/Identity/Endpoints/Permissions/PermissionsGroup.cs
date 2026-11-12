namespace Backend.Features.Identity.Endpoints.Permissions;

/// <summary>
/// This route group that prefixes all permission-management endpoints with the
/// <c>permissions</c> URL segment (e.g. listing defined or stored permissions).
/// </summary>
sealed class PermissionsGroup : Group
{
    public PermissionsGroup()
    {
        Configure("permissions", ep => {});
    }
}