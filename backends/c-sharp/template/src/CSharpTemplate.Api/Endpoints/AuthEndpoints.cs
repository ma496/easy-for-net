using CSharpTemplate.Common.Identity;

namespace CSharpTemplate.Api.Endpoints;

public static class AuthEndpoints
{
    public static void Register(WebApplication app)
    {
        var group = app.MapGroup("/auth").WithTags("Auth");

        group.MapPost("/register", async (RegisterUserInput input, IAuthManager authManager) => 
        {
            await authManager.RegisterUserAsync(input);
        });

        group.MapPost("/login", async (LoginUserInput input, IAuthManager authManager) =>
        {
            var result = await authManager.LoginUserAsync(input);
            return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
        }).Produces<LoginUserOutput>()
            .Produces<LoginUserOutput>(400);
    }
}
