namespace Backend.Middleware;

using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>
/// The body <c>/health</c> answers with in Development: the status, and the checkout the host was
/// started from.
/// </summary>
/// <remarks>
/// <para>
/// A live check is evidence only if it exercised the code under test, and something answering on the
/// expected port does not prove that - a server left running from another checkout answers just as
/// readily. So in Development the health body names the repository root it runs from, and the
/// verification script adopts a running API only when that is the checkout it is verifying.
/// </para>
/// <para>
/// Every other environment keeps the framework's plain response: a path on the server's disk is of no
/// use to anyone outside it and says more about the machine than a health check should.
/// </para>
/// </remarks>
public static class DevelopmentHealthResponse
{
    /// <summary>
    /// The options that make <c>/health</c> report the repository root found above
    /// <paramref name="contentRootPath"/>.
    /// </summary>
    /// <param name="contentRootPath">The host's content root.</param>
    /// <returns>Health check options carrying the Development response writer.</returns>
    public static HealthCheckOptions Options(string contentRootPath)
    {
        var repoRoot = FindRepositoryRoot(contentRootPath);
        return new HealthCheckOptions { ResponseWriter = (context, report) => WriteAsync(context, report, repoRoot) };
    }

    /// <summary>
    /// The nearest directory at or above <paramref name="start"/> that holds a <c>.git</c> entry - a
    /// directory in a normal checkout, a file in a worktree - or <paramref name="start"/> itself when
    /// there is none.
    /// </summary>
    /// <param name="start">The directory to search upwards from.</param>
    /// <returns>The repository root, as a full path.</returns>
    public static string FindRepositoryRoot(string start)
    {
        var fullStart = Path.GetFullPath(start);
        for (var dir = new DirectoryInfo(fullStart); dir is not null; dir = dir.Parent)
        {
            var marker = Path.Combine(dir.FullName, ".git");
            if (Directory.Exists(marker) || File.Exists(marker))
            {
                return dir.FullName;
            }
        }

        return fullStart;
    }

    /// <summary>
    /// Writes <c>{ "status": ..., "repoRoot": ... }</c> as the health response.
    /// </summary>
    /// <param name="context">The request being answered.</param>
    /// <param name="report">The health report to summarise.</param>
    /// <param name="repoRoot">The checkout the host runs from.</param>
    /// <returns>A task that completes when the body has been written.</returns>
    public static Task WriteAsync(HttpContext context, HealthReport report, string repoRoot)
    {
        context.Response.ContentType = "application/json";
        var body = JsonSerializer.Serialize(new { status = report.Status.ToString(), repoRoot });
        return context.Response.WriteAsync(body);
    }
}
