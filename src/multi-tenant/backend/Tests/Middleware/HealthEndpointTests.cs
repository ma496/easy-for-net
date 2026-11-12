namespace Backend.Tests.Middleware;

/// <summary>
/// Tests that <c>/health</c> outside Development answers with the framework's plain status and no
/// path from the server's disk.
/// </summary>
public class HealthEndpointTests(App app) : AppTestsBase(app)
{
    /// <summary>
    /// Verifies that an anonymous caller gets the plain status, with no repository root in it.
    /// </summary>
    [Fact]
    public async Task Answers_Plain_Status_Without_Repository_Root()
    {
        var response = await Client.GetAsync("/health", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Be("Healthy");
        body.Should().NotContain("repoRoot");
    }
}
