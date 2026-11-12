namespace Backend.Features.Identity.Endpoints.Roles;

/// <summary>
/// This route group that prefixes all role-management endpoints with the <c>roles</c> segment.
/// </summary>
sealed class RolesGroup : Group
{
    public RolesGroup()
    {
        Configure("roles", ep => {});
    }
}


