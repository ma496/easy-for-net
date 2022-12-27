using CSharpTemplate.Common.Identity;

namespace CSharpTemplate.Api.Endpoints;

public static class AuthEndpoints
{
    public static void Register(WebApplication app)
    {
        var group = app.MapGroup("/auth");

        group.MapPost("/register", async (RegisterUserInput input, IAuthManager authManager) => 
        {
            var result = await authManager.RegisterUserAsync(input);
            if (result.IsSuccess)
                return Results.Ok(result);
            else
                return Results.BadRequest(result);
        });

        group.MapPost("/login", async (LoginUserInput input, IAuthManager authManager) =>
        {
            var result = await authManager.LoginUserAsync(input);
            if (result.IsSuccess)
                return Results.Ok(result);
            else
                return Results.BadRequest(result);
        });
    }
}
