using System.Text.Json;
using System.Text.Json.Serialization;
using Backend.Features.Localization.Core;

namespace Backend.ErrorHandling;

/// <summary>
/// Puts a <see cref="ProblemDetails"/> into the request's culture just before FastEndpoints writes it.
/// FastEndpoints 8 hands <c>GlobalResponseModifierAsync</c> <c>null</c> for every response, so the one
/// seam that still sees the error body is the response serializer: it is given the object about to be
/// written, which is the same instance the endpoint's error path built (validation failures, a
/// <c>ThrowError</c> call, and <c>SendErrorsAsync</c> for the permission refusal alike). Everything that is
/// not a ProblemDetails is written exactly as FastEndpoints' own default writes it.
/// </summary>
internal static class LocalizingResponseSerializer
{
    public static Func<HttpResponse, object?, string, JsonSerializerContext?, CancellationToken, Task> Create(
        JsonSerializerOptions defaultOptions) =>
        async (rsp, dto, contentType, jsonContext, cancellation) =>
        {
            if (dto is null)
            {
                return;
            }

            if (dto is ProblemDetails problemDetails)
            {
                await LocalizeAsync(rsp.HttpContext, problemDetails);
            }

            // What FastEndpoints' default serializer does: JSON through the configured options, with
            // the charset appended to the content type.
            var type = contentType.Contains("charset", StringComparison.OrdinalIgnoreCase)
                ? contentType
                : contentType + "; charset=utf-8";
            await rsp.WriteAsJsonAsync(dto, jsonContext?.Options ?? defaultOptions, type, cancellation);
        };

    private static async Task LocalizeAsync(HttpContext httpContext, ProblemDetails problemDetails)
    {
        var localizer = httpContext.RequestServices.GetRequiredService<IErrorMessageLocalizer>();
        foreach (var error in problemDetails.Errors)
        {
            error.Reason = await localizer.LocalizeErrorAsync(
                httpContext, error.Code, error.Name, error.Reason, httpContext.RequestAborted);
        }

        // Detail was computed once at construction from the English reasons; recomputed so a
        // single-error response's `detail` carries the same localized text `errors` now does.
        if (problemDetails.Errors.Count() == 1)
        {
            problemDetails.Detail = problemDetails.Errors.First().Reason;
        }
    }
}
