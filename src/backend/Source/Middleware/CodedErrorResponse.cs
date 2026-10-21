using Backend.Features.Localization.Core;

namespace Backend.Middleware;

/// <summary>
/// Writes a coded error response from middleware that runs ahead of FastEndpoints: the shape
/// <c>ExceptionProcessor</c> writes for every other hand-built error, with the message localized into the
/// request's culture through <see cref="IErrorMessageLocalizer"/>.
/// </summary>
public static class CodedErrorResponse
{
    /// <summary>
    /// Writes the response and completes it.
    /// </summary>
    /// <param name="context">The request being answered.</param>
    /// <param name="localizer">Localizes the refusal into the request's culture.</param>
    /// <param name="statusCode">The status to answer with: 403 or 503.</param>
    /// <param name="code">The error code the response carries.</param>
    /// <param name="cancellationToken">Token used to cancel the write.</param>
    public static async Task WriteAsync(HttpContext context, IErrorMessageLocalizer localizer, int statusCode, ErrorCode code, CancellationToken cancellationToken)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var reason = await localizer.LocalizeErrorAsync(
            context,
            code.Value,
            string.Empty,
            context.ResolveEnglishFallback(code),
            cancellationToken);

        var (type, title) = statusCode switch
        {
            StatusCodes.Status403Forbidden => ("https://www.rfc-editor.org/rfc/rfc7231#section-6.5.3", "Forbidden"),
            StatusCodes.Status503ServiceUnavailable => ("https://www.rfc-editor.org/rfc/rfc7231#section-6.6.4", "Service Unavailable"),
            _ => throw new ArgumentOutOfRangeException(nameof(statusCode), statusCode, "Only 403 and 503 are written by middleware."),
        };

        await context.Response.WriteAsJsonAsync(new
        {
            type,
            title,
            status = statusCode,
            instance = context.Request.Path.Value,
            traceId = context.TraceIdentifier,
            errors = new[] { new { reason, code = code.Value } }
        }, cancellationToken: cancellationToken);
    }
}
