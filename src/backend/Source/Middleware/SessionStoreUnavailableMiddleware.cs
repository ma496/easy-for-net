namespace Backend.Middleware;

using Backend.Features.Localization.Core;

/// <summary>
/// Answers 503 <c>sessionStoreUnavailable</c> for a request whose session could not be looked up,
/// instead of letting it continue as an anonymous caller.
/// </summary>
/// <remarks>
/// <para>
/// The authentication handlers swallow an exception raised while validating a token into a plain
/// authentication failure, which the pipeline reads as "anonymous" and answers 401 - or, on an
/// endpoint that allows anonymous callers, serves. A store outage is neither: the session may be
/// perfectly valid. The session validator therefore records the outage on the request and fails
/// authentication, and this middleware, placed immediately after <c>UseAuthentication()</c>, turns the
/// record into the refusal and short-circuits. It is a refusal, never a fall-through.
/// </para>
/// <para>
/// The body has the shape <c>ExceptionProcessor</c> writes for every other hand-built error, and the
/// message is localized through <see cref="IErrorMessageLocalizer"/> like theirs.
/// </para>
/// </remarks>
/// <param name="next">The next middleware in the pipeline.</param>
public sealed class SessionStoreUnavailableMiddleware(RequestDelegate next)
{
    /// <summary>
    /// The <see cref="HttpContext.Items"/> key the session validator sets when the store could not be
    /// reached.
    /// </summary>
    public const string OutageItemKey = "Backend.SessionStoreUnavailable";

    /// <summary>
    /// Refuses the request when the session store was unreachable while authenticating it, and
    /// otherwise passes it on.
    /// </summary>
    /// <param name="context">The request being handled.</param>
    /// <param name="localizer">Localizes the refusal into the request's culture.</param>
    public async Task InvokeAsync(HttpContext context, IErrorMessageLocalizer localizer)
    {
        if (context.Items.ContainsKey(OutageItemKey))
        {
            await WriteAsync(context, localizer, context.RequestAborted);
            return;
        }

        await next(context);
    }

    /// <summary>
    /// Writes the 503 <c>sessionStoreUnavailable</c> response. Shared with
    /// <see cref="Backend.Processors.ExceptionProcessor"/>, so a store failure while minting a session
    /// (sign-in, refresh, switch, sign-out) is answered exactly like one while validating.
    /// </summary>
    /// <param name="context">The request being answered.</param>
    /// <param name="localizer">Localizes the refusal into the request's culture.</param>
    /// <param name="cancellationToken">Token used to cancel the write.</param>
    public static async Task WriteAsync(HttpContext context, IErrorMessageLocalizer localizer, CancellationToken cancellationToken)
    {
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        context.Response.ContentType = "application/json";

        var reason = await localizer.LocalizeErrorAsync(
            context,
            ErrorCodes.SessionStoreUnavailable.Value,
            string.Empty,
            context.ResolveEnglishFallback(ErrorCodes.SessionStoreUnavailable),
            cancellationToken);

        await context.Response.WriteAsJsonAsync(new
        {
            type = "https://www.rfc-editor.org/rfc/rfc7231#section-6.6.4",
            title = "Service Unavailable",
            status = 503,
            instance = context.Request.Path.Value,
            traceId = context.TraceIdentifier,
            errors = new[] { new { reason, code = ErrorCodes.SessionStoreUnavailable.Value } }
        }, cancellationToken: cancellationToken);
    }
}
