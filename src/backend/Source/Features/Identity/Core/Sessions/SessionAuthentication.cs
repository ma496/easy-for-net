namespace Backend.Features.Identity.Core.Sessions;

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;

/// <summary>
/// Hooks session validation into both authentication handlers, so a bearer token and an auth cookie
/// are held to the same rule: the session they name must exist in the session store.
/// </summary>
/// <remarks>
/// The handlers' own events are chained rather than replaced - FastEndpoints sets cookie events of its
/// own so an unauthenticated API call answers 401 instead of redirecting to a login page - and the
/// original always runs first, so a token the handler already rejected is never looked up.
/// </remarks>
public static class SessionAuthentication
{
    /// <summary>
    /// Adds session validation to the bearer and cookie handlers through post-configuration, which runs
    /// after every registration that configures their events.
    /// </summary>
    /// <param name="services">The service collection to extend.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddSessionValidation(this IServiceCollection services)
    {
        services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            options.Events ??= new JwtBearerEvents();
            var previous = options.Events.OnTokenValidated;
            options.Events.OnTokenValidated = async context =>
            {
                await previous(context);
                if (context.Result is not null || context.Principal is null)
                {
                    return;
                }

                var validator = context.HttpContext.RequestServices.GetRequiredService<ISessionPrincipalValidator>();
                if (!await validator.ValidateAsync(context.HttpContext, context.Principal))
                {
                    context.Fail("The session is no longer valid.");
                }
            };
        });

        services.PostConfigure<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme, options =>
        {
            options.Events ??= new CookieAuthenticationEvents();
            var previous = options.Events.OnValidatePrincipal;
            options.Events.OnValidatePrincipal = async context =>
            {
                await previous(context);
                if (context.Principal is null)
                {
                    return;
                }

                var validator = context.HttpContext.RequestServices.GetRequiredService<ISessionPrincipalValidator>();
                if (!await validator.ValidateAsync(context.HttpContext, context.Principal))
                {
                    context.RejectPrincipal();
                }
            };
        });

        return services;
    }
}
