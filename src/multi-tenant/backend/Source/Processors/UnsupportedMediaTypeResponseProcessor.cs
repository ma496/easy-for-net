namespace Backend.Processors;

/// <summary>
/// Global FastEndpoints post-processor that, when a request is rejected with a 415
/// Unsupported Media Type status code, replaces the response body with a JSON
/// payload in the project's standard error format.
/// </summary>
public class UnsupportedMediaTypeResponseProcessor : IGlobalPostProcessor
{
    /// <summary>
    /// Detects a 415 response status and rewrites the body to the standard
    /// JSON error envelope.
    /// </summary>
    public async Task PostProcessAsync(IPostProcessorContext context, CancellationToken ct)
    {
        if (context.HttpContext.Response.StatusCode == StatusCodes.Status415UnsupportedMediaType)
        {
            context.HttpContext.Response.ContentType = "application/json";
            var response = new
            {
                type = "https://www.rfc-editor.org/rfc/rfc7231#section-6.5.13",
                title = "Unsupported Media Type",
                status = StatusCodes.Status415UnsupportedMediaType,
                instance = context.HttpContext.Request.Path.Value,
                traceId = context.HttpContext.TraceIdentifier,
                errors = new[] { new { name = "Unsupported Media Type", reason = "The media type is not supported." } }
            };

            await context.HttpContext.Response.WriteAsJsonAsync(response, cancellationToken: ct);
        }
    }
}
