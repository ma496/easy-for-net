namespace Backend.Middleware;

using System.Diagnostics;

/// <summary>
/// Logs one line per matched FastEndpoint during development - method, path, status and time - the
/// way the web app's dev server logs a page.
/// </summary>
public sealed class DevelopmentEndpointLoggingMiddleware(
    RequestDelegate next,
    ILogger<DevelopmentEndpointLoggingMiddleware> logger)
{
    /// <summary>
    /// Times the matched FastEndpoint and logs it once it has answered. The query string is left out:
    /// a bearer client connecting to the notification hub passes its access token there.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.GetEndpoint()?.Metadata.GetMetadata<EndpointDefinition>() == null)
        {
            await next(context);
            return;
        }

        var startedAt = Stopwatch.GetTimestamp();

        try
        {
            await next(context);
        }
        finally
        {
            logger.LogInformation(
                "{HttpMethod} {Path} {StatusCode} in {ElapsedMilliseconds:F0}ms",
                context.Request.Method,
                context.Request.Path.Value ?? "/",
                context.Response.StatusCode,
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        }
    }
}
