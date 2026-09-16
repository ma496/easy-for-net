namespace Backend;

using System.Security.Claims;
using Backend.Features.Identity.Core;
using Hangfire.Dashboard;

/// <summary>
/// Restricts the Hangfire dashboard to authenticated callers that hold the
/// platform administration permission, keeping the background-job surface on the
/// platform tier so it cannot be reached from inside a tenant.
/// </summary>
public sealed class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    /// <summary>
    /// Grants dashboard access only when the request comes from an authenticated
    /// caller whose permission claims contain
    /// <see cref="Allow.Platform_Administration"/>; returns <c>false</c> for
    /// everyone else.
    /// </summary>
    /// <param name="context">The current Hangfire dashboard context.</param>
    /// <returns><c>true</c> if the request should be allowed; otherwise <c>false</c>.</returns>
    public bool Authorize(DashboardContext context)
        => IsAuthorized(context.GetHttpContext().User);

    /// <summary>
    /// The decision itself, taken over the caller's principal rather than over the
    /// dashboard context that carries it, so the gate can be exercised as the pure
    /// rule it is - one principal in, one answer out - instead of through a
    /// fabricated dashboard request.
    /// </summary>
    /// <param name="user">The principal the request was authenticated as.</param>
    /// <returns><c>true</c> if the caller may open the dashboard; otherwise <c>false</c>.</returns>
    internal static bool IsAuthorized(ClaimsPrincipal user)
    {
        // An unauthenticated caller carries no permission claims at all.
        if (user.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        // The tier test is the permission claim, never a role name: role names are
        // per tenant, so any tenant could otherwise define a role called "Admin"
        // and reach a platform-wide operational surface from inside its own scope.
        return user.HasClaim(ClaimConstants.Permission, Allow.Platform_Administration);
    }
}
