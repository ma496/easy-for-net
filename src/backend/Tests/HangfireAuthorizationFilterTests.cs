namespace Backend.Tests;

using System.Security.Claims;
using Backend.Features.Identity.Core;

/// <summary>
/// Tests that the background-job dashboard is a platform-tier surface: an unauthenticated caller and a
/// caller holding only a tenant's own authority are both refused, and what opens it is the account tier
/// and nothing else - a tenant-defined role named <c>Admin</c> and any permission a tenant could grant
/// included.
/// </summary>
/// <remarks>
/// The rule is exercised over principals rather than through a fabricated dashboard request, which is
/// what makes the case that matters - a tenant administrator whose role happens to be called
/// <c>Admin</c> - expressible at all: the point of the gate is that a role name is never what opens a
/// platform-wide operational surface, because every tenant may define a role of any name inside its own
/// scope. The tier is read rather than a permission for a second reason: a permission would be narrowed
/// away the moment a platform account entered a tenant, and the dashboard is not a tenant's to grant.
/// </remarks>
public class HangfireAuthorizationFilterTests
{
    /// <summary>
    /// Verifies that the dashboard is open to an authenticated platform account and to nobody else
    ///.
    /// </summary>
    [Fact]
    public void Dashboard_Requires_Platform_Account()
    {
        HangfireAuthorizationFilter.IsAuthorized(Principal(authenticated: false))
            .Should().BeFalse("an unauthenticated caller carries no claim at all");

        HangfireAuthorizationFilter.IsAuthorized(
                Principal(authenticated: false, PlatformTier()))
            .Should().BeFalse("the claim is not what authenticates a caller: an unauthenticated principal opens nothing");

        HangfireAuthorizationFilter.IsAuthorized(
                Principal(authenticated: true, new Claim(ClaimTypes.Role, "Admin")))
            .Should().BeFalse("a tenant may define a role named Admin, so a role name is never a platform tier");

        HangfireAuthorizationFilter.IsAuthorized(
                Principal(authenticated: true, Permission(Allow.Tenant_View), new Claim(ClaimTypes.Role, "Admin")))
            .Should().BeFalse("no permission opens the dashboard, whatever tier it belongs to, and an Administrator role inside a tenant is the same answer");

        HangfireAuthorizationFilter.IsAuthorized(
                Principal(authenticated: true, PlatformTier()))
            .Should().BeTrue("the platform tier is the one standing the dashboard is gated on");
    }

    /// <summary>
    /// Builds the principal a request would be authenticated as, so a case reads as the caller it
    /// describes rather than as the identity objects behind it.
    /// </summary>
    /// <param name="authenticated">Whether the identity authenticated at all.</param>
    /// <param name="claims">The claims it carries.</param>
    /// <returns>The principal.</returns>
    private static ClaimsPrincipal Principal(bool authenticated, params Claim[] claims)
        => new(new ClaimsIdentity(claims, authenticationType: authenticated ? "Test" : null));

    /// <summary>
    /// A permission claim in the shape the application issues - the claim type the filter reads, so a
    /// case cannot pass by carrying its permission under a claim type nothing looks at.
    /// </summary>
    /// <param name="permission">The permission name.</param>
    /// <returns>The claim.</returns>
    private static Claim Permission(string permission) => new(ClaimConstants.Permission, permission);

    /// <summary>
    /// The account-tier claim in the shape the application issues it.
    /// </summary>
    /// <returns>The claim.</returns>
    private static Claim PlatformTier() => new(ClaimConstants.IsPlatform, bool.TrueString);
}
