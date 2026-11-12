using Backend.Tests.Seeder;

namespace Backend.Tests.Features.BackgroundJobs;

/// <summary>
/// Tests the background-job dashboard as the endpoint it actually is: a request to the path it is
/// mounted on, over HTTP, answered by the pipeline the application runs.
/// </summary>
/// <remarks>
/// What opens the dashboard is <see cref="Backend.Permissions.Allow.Hangfire_Dashboard"/>: the seeded <c>limited</c> account,
/// signed in and holding one other permission, is refused, and the administrator, whose role holds every
/// permission, is served. <c>HangfireAuthorizationFilterTests</c> exercises the decision itself over principals.
/// </remarks>
public class HangfireDashboardTests(App app) : AppTestsBase(app)
{
    /// <summary>The path the dashboard is mounted on.</summary>
    private const string DashboardPath = "/hangfire";

    /// <summary>
    /// Verifies that a caller without the dashboard permission is refused the background-job
    /// dashboard, and that the administrator is served it.
    /// </summary>
    [Fact]
    public async Task Refuses_Every_Caller_Without_The_Dashboard_Permission()
    {
        ClearAuthToken();

        var anonymous = await Client.GetAsync(DashboardPath, TestContext.Current.CancellationToken);

        anonymous.StatusCode.Should().Be(
            HttpStatusCode.Unauthorized,
            "an unauthenticated caller holds no permission at all, and is challenged rather than told the surface exists");

        // Signed in with a real session, holding a permission - just not the dashboard one.
        await SetAuthTokenAsync("limited", TestUsers.DefaultPassword);

        var limitedCaller = await Client.GetAsync(DashboardPath, TestContext.Current.CancellationToken);

        limitedCaller.StatusCode.Should().Be(
            HttpStatusCode.Forbidden,
            "an authenticated caller without the dashboard permission is not enough");

        await SetAuthTokenAsync();

        var administrator = await Client.GetAsync(DashboardPath, TestContext.Current.CancellationToken);

        administrator.StatusCode.Should().Be(
            HttpStatusCode.OK,
            "the dashboard permission opens it, which is what makes the refusals above the gate's doing rather than the path answering nothing");

        var body = await administrator.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        body.Should().Contain("<html", "what the administrator is served is the dashboard rather than an empty page");
    }
}
