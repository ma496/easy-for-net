using Backend.Features.Localization.Core;
using Backend.Settings;

namespace Backend.Processors;

/// <summary>
/// Global FastEndpoints pre-processor that rejects incoming requests whose declared
/// content length exceeds the configured maximum payload size, returning a 413
/// Payload Too Large response in the project's standard error format.
/// </summary>
/// <remarks>
/// Its message is localized through <see cref="IErrorMessageLocalizer"/>, exactly as every other error
/// response is. A global processor is built once,
/// as a singleton, so the scoped service is resolved from the request's own
/// <see cref="HttpContext.RequestServices"/> rather than taken as a constructor dependency, which the
/// root provider would refuse to construct at all.
/// </remarks>
public class ToLargePayloadProcessor(IOptions<PayloadSetting> payloadOptions) : IGlobalPreProcessor
{
    /// <summary>
    /// Inspects the request's content length and, when it exceeds the configured
    /// maximum, short-circuits the pipeline with a 413 response payload.
    /// </summary>
    public async Task PreProcessAsync(IPreProcessorContext context, CancellationToken ct)
    {
        var maxSize = payloadOptions.Value.MaximumSize;
        var contentLength = context.HttpContext.Request.ContentLength;
        if (contentLength is { } length && length > maxSize)
        {
            context.HttpContext.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            context.HttpContext.Response.ContentType = "application/json";
            var localizationService = context.HttpContext.RequestServices.GetRequiredService<IErrorMessageLocalizer>();
            var fallback = context.HttpContext.ResolveEnglishFallback(ErrorCodes.PayloadTooLarge);
            var reason = await localizationService.LocalizeErrorAsync(
                context.HttpContext, ErrorCodes.PayloadTooLarge.Value, "Payload", fallback, ct);
            var response = new
            {
                type = "https://www.rfc-editor.org/rfc/rfc7231#section-6.5.11",
                title = "Payload Too Large",
                status = StatusCodes.Status413PayloadTooLarge,
                instance = context.HttpContext.Request.Path.Value,
                traceId = context.HttpContext.TraceIdentifier,
                errors = new[] { new { name = "Payload", reason, code = ErrorCodes.PayloadTooLarge.Value } }
            };
            await context.HttpContext.Response.WriteAsJsonAsync(response, cancellationToken: ct);
        }
    }
}