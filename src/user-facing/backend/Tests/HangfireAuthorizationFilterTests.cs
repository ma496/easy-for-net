using System.Security.Claims;
using Backend.Features.Identity.Core;

namespace Backend.Tests;

/// <summary>
/// Tests that the background-job dashboard opens to an authenticated caller holding
/// <see cref="Allow.Hangfire_Dashboard"/> and to nobody else - a role name and any other permission
/// included.
/// </summary>
/// <remarks>
/// The rule is exercised over principals rather than through a fabricated dashboard request, so each
/// case reads as the caller it describes; <c>HangfireDashboardTests</c> proves the rule is wired into the
/// pipeline.
/// </remarks>
public class HangfireAuthorizationFilterTests
{
    [Fact]
    public void Dashboard_Requires_The_Dashboard_Permission()
    {
        HangfireAuthorizationFilter.IsAuthorized(Principal(authenticated: false))
            .Should().BeFalse("an unauthenticated caller carries no claim at all");

        HangfireAuthorizationFilter.IsAuthorized(
                Principal(authenticated: false, Permission(Allow.Hangfire_Dashboard)))
            .Should().BeFalse("the claim is not what authenticates a caller: an unauthenticated principal opens nothing");

        HangfireAuthorizationFilter.IsAuthorized(
                Principal(authenticated: true, new Claim(ClaimTypes.Role, "Admin")))
            .Should().BeFalse("a role name is never an authorization input");

        HangfireAuthorizationFilter.IsAuthorized(
                Principal(authenticated: true, Permission(Allow.Admin_Access), Permission(Allow.Settings_Update)))
            .Should().BeFalse("no other permission opens the dashboard");

        HangfireAuthorizationFilter.IsAuthorized(
                Principal(authenticated: true, Permission(Allow.Hangfire_Dashboard)))
            .Should().BeTrue("the dashboard permission is what the dashboard is gated on");
    }

    private static ClaimsPrincipal Principal(bool authenticated, params Claim[] claims)
        => new(new ClaimsIdentity(claims, authenticationType: authenticated ? "Test" : null));

    /// <summary>
    /// A permission claim in the shape the application issues - the claim type the filter reads, so a
    /// case cannot pass by carrying its permission under a claim type nothing looks at.
    /// </summary>
    private static Claim Permission(string permission) => new(ClaimConstants.Permission, permission);
}
