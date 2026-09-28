namespace Backend.Tests.Middleware;

using System.Text.Json;
using Backend.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>
/// Tests the Development health body: which directory it names as the checkout, and what it writes.
/// </summary>
public class DevelopmentHealthResponseTests
{
    /// <summary>
    /// Verifies that the root is the nearest ancestor holding a <c>.git</c> directory, however deep the
    /// content root sits beneath it.
    /// </summary>
    [Fact]
    public void Finds_The_Nearest_Directory_Holding_A_Git_Directory()
    {
        var root = CreateTempDirectory();
        Directory.CreateDirectory(Path.Combine(root, ".git"));
        var contentRoot = Directory.CreateDirectory(Path.Combine(root, "src", "backend", "Source")).FullName;

        DevelopmentHealthResponse.FindRepositoryRoot(contentRoot).Should().Be(Path.GetFullPath(root));
    }

    /// <summary>
    /// Verifies that a worktree - where <c>.git</c> is a file rather than a directory - is recognised as
    /// a checkout root too.
    /// </summary>
    [Fact]
    public void Recognises_A_Worktree_Whose_Git_Entry_Is_A_File()
    {
        var root = CreateTempDirectory();
        File.WriteAllText(Path.Combine(root, ".git"), "gitdir: elsewhere");
        var contentRoot = Directory.CreateDirectory(Path.Combine(root, "src")).FullName;

        DevelopmentHealthResponse.FindRepositoryRoot(contentRoot).Should().Be(Path.GetFullPath(root));
    }

    /// <summary>
    /// Verifies that the body carries the status and the repository root as JSON.
    /// </summary>
    [Fact]
    public async Task Writes_Status_And_Repository_Root()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var report = new HealthReport(new Dictionary<string, HealthReportEntry>(), TimeSpan.Zero);

        await DevelopmentHealthResponse.WriteAsync(context, report, "/repo");

        context.Response.ContentType.Should().Be("application/json");
        context.Response.Body.Position = 0;
        using var body = await JsonDocument.ParseAsync(context.Response.Body, cancellationToken: TestContext.Current.CancellationToken);
        body.RootElement.GetProperty("status").GetString().Should().Be("Healthy");
        body.RootElement.GetProperty("repoRoot").GetString().Should().Be("/repo");
    }

    private static string CreateTempDirectory() =>
        Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"health-{Guid.NewGuid():N}")).FullName;
}
