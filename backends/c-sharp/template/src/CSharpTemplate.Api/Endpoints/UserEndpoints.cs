using CSharpTemplate.Common.Identity;
using CSharpTemplate.Common.Identity.Dto;

namespace CSharpTemplate.Api.Endpoints;

public static class UserEndpoints
{
    public static void Register(RouteGroupBuilder root)
    {
        var group = root.MapGroup("/user").WithTags("user");
        
        group.MapGet("/get-by-id/{id}", async (long id, IUserManager userManager) 
            => await userManager.GetByIdAsync(id)).Produces<UserDto>();

        group.MapGet("/get-by-email/{email}", async (string email, IUserManager userManager) 
            => await userManager.GetByEmailAsync(email)).Produces<UserDto>();
    }
}