namespace Backend.Middleware;

using Backend.Features.Identity.Core;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;

/// <summary>
/// Removes authentication established before the user's most recent password change.
/// </summary>
public class SessionValidationMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, AppDbContext dbContext)
    {
        if (context.User.Identity?.IsAuthenticated == true &&
            !await SessionValidator.IsCurrentAsync(context.User, dbContext, context.RequestAborted))
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity());
            context.Response.Cookies.Delete("refreshToken");
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }

        await next(context);
    }
}
