using System.Security.Claims;
using Hangfire.Dashboard;

namespace Backend;

/// <summary>
/// Restricts the Hangfire dashboard to authenticated callers whose session holds
/// <see cref="Allow.Hangfire_Dashboard"/>.
/// </summary>
public sealed class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    /// <summary>
    /// Grants dashboard access only when the request comes from an authenticated caller holding the
    /// dashboard permission; returns <c>false</c> for everyone else.
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
        // An unauthenticated caller carries no claims at all.
        if (user.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        // A permission, never a role name: permissions are the only authorization input. The claim is
        // the one the session projects onto the principal, so a revoked or changed grant reaches the
        // dashboard as it reaches every endpoint.
        return user.HasClaim("permission", Allow.Hangfire_Dashboard);
    }
}
