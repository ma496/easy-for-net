namespace Backend.Features.Identity.Endpoints.Users;

/// <summary>
/// This route group that prefixes all user-management endpoints with the <c>users</c> segment.
/// </summary>
sealed class UsersGroup : Group
{
    public UsersGroup()
    {
        Configure("users", ep => {});
    }
}


