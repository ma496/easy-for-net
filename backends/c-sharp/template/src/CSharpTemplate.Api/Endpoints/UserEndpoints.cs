using CSharpTemplate.Api.Endpoints.Automation;
using CSharpTemplate.Common.Identity;
using CSharpTemplate.Common.Identity.Permissions;

namespace CSharpTemplate.Api.Endpoints;

public static class UserEndpoints
{
    [MinimalApi]
    public static void Register(RouteGroupBuilder root)
    {
        var group = root.MapGroup("/user").WithTags("User");
        
        group.MapGet("/get-by-id/{id}", async (long id, IUserManager userManager) 
            => await userManager.GetByIdAsync(id))
            .RequireAuthorization(IdentityPermissions.Users);

        group.MapGet("/get-by-email/{email}", async (string email, IUserManager userManager) 
            => await userManager.GetByEmailAsync(email))
            .RequireAuthorization(IdentityPermissions.Users);

        group.MapGet("/get-list", async (IUserManager userManager)
            => await userManager.GetListAsync())
            .RequireAuthorization(IdentityPermissions.Users);
    }
}