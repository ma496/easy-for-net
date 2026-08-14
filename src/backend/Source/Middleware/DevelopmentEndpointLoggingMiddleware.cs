namespace Backend.Middleware;

using System.Diagnostics;

/// <summary>
/// Logs entry, exit, and execution time for matched FastEndpoints during development.
/// </summary>
public sealed class DevelopmentEndpointLoggingMiddleware(
    RequestDelegate next,
    ILogger<DevelopmentEndpointLoggingMiddleware> logger)
{
    /// <summary>
    /// Logs safe request diagnostics around the execution of a matched FastEndpoint.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        if (endpoint?.Metadata.GetMetadata<EndpointDefinition>() == null)
        {
            await next(context);
            return;
        }

        var endpointName = endpoint.DisplayName ?? "UnknownEndpoint";
        var method = context.Request.Method;
        var path = context.Request.Path.Value ?? "/";
        var traceId = context.TraceIdentifier;

        logger.LogInformation(
            "Entering endpoint {EndpointName}: {HttpMethod} {Path} [TraceId: {TraceId}]",
            endpointName,
            method,
            path,
            traceId);

        var startedAt = Stopwatch.GetTimestamp();

        try
        {
            await next(context);
        }
        finally
        {
            var elapsedMilliseconds = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
            logger.LogInformation(
                "Exiting endpoint {EndpointName}: {HttpMethod} {Path} returned {StatusCode} in {ElapsedMilliseconds:F2} ms [TraceId: {TraceId}]",
                endpointName,
                method,
                path,
                context.Response.StatusCode,
                elapsedMilliseconds,
                traceId);
        }
    }
}
