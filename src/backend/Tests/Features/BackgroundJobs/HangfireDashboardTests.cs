namespace Backend.Tests.Features.BackgroundJobs;

using Backend.Tests.Seeder;

/// <summary>
/// Tests the background-job dashboard as the endpoint it actually is: a request to the path it is
/// mounted on, over HTTP, answered by the pipeline the application runs (AC-047).
/// </summary>
/// <remarks>
/// <para>
/// What opens the dashboard is the platform administration permission, and the caller that matters is
/// the one holding a tenant's own authority: a tenant may define a role of any name inside its own
/// scope, so no role name and no tenant-tier permission may reach a platform-wide operational surface.
/// The seeded <c>limited</c> account is exactly that caller - a member of the bootstrap tenant holding
/// one tenant-tier permission and nothing else.
/// </para>
/// <para>
/// The badge of a platform administrator reaching the dashboard is asserted alongside the refusals,
/// because a refusal is only evidence of a gate if the surface is there to be gated: without it, every
/// refusal below would be satisfied by a path that answers nothing to anybody.
/// </para>
/// <para>
/// This is the HTTP half of the rule. <c>HangfireAuthorizationFilterTests</c> exercises the decision
/// itself over principals, where the case that matters - a tenant administrator whose role happens to be
/// called <c>Admin</c> - is expressible directly; this test is what proves the decision is wired into
/// the pipeline rather than merely implemented.
/// </para>
/// </remarks>
public class HangfireDashboardTests(App app) : AppTestsBase(app)
{
    /// <summary>The path the dashboard is mounted on.</summary>
    private const string DashboardPath = "/hangfire";

    /// <summary>
    /// Verifies that a caller who is not a platform administrator is refused the background-job
    /// dashboard, and that a platform administrator is served it (AC-047).
    /// </summary>
    [Fact]
    public async Task Refuses_Every_Caller_Without_Platform_Administration()
    {
        ClearAuthToken();

        var anonymous = await App.Client.GetAsync(DashboardPath, TestContext.Current.CancellationToken);

        anonymous.StatusCode.Should().Be(
            HttpStatusCode.Unauthorized,
            "an unauthenticated caller holds no permission at all, and is challenged rather than told the surface exists");

        // A member of a tenant holding a tenant-tier permission, signed in with a real session: the
        // standing that must not reach a platform-wide operational surface.
        await SetAuthTokenAsync("limited", TestUsers.DefaultPassword);

        var tenantCaller = await App.Client.GetAsync(DashboardPath, TestContext.Current.CancellationToken);

        tenantCaller.StatusCode.Should().Be(
            HttpStatusCode.Forbidden,
            "a tenant's own authority is authenticated and still not enough, because the dashboard is on the platform tier");

        await SetPlatformAdminAuthTokenAsync();

        var platformAdministrator = await App.Client.GetAsync(DashboardPath, TestContext.Current.CancellationToken);

        platformAdministrator.StatusCode.Should().Be(
            HttpStatusCode.OK,
            "platform administration opens the dashboard, which is what makes the refusals above the gate's doing rather than the path answering nothing");

        var body = await platformAdministrator.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        body.Should().Contain("<html", "what a platform administrator is served is the dashboard rather than an empty page");
    }
}
