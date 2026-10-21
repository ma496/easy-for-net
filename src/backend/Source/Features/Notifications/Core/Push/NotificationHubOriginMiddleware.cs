using Backend.Settings;
using Microsoft.Net.Http.Headers;

namespace Backend.Features.Notifications.Core.Push;

/// <summary>
/// Refuses, with 403, a request to the notification hub whose <c>Origin</c> is neither the request's own
/// origin nor one of the web app's configured domains.
/// </summary>
/// <remarks>
/// <para>
/// A WebSocket upgrade is not a CORS request, so the CORS policy does not stop a page on another origin -
/// a sibling host on the same site, whose requests carry the auth cookie - from opening the hub as its
/// visitor. This check is what does. With tenants served on hosts of their own the allowed origins are
/// no longer a fixed list: the web app reaches the API same-origin on each tenant's host, so an upgrade
/// is admitted when its <c>Origin</c> is the origin the request itself was made to (scheme and host, as
/// the forwarded headers leave them) or one of <see cref="WebSetting.AllowedDomains"/>. An upgrade with
/// no <c>Origin</c> at all (a non-browser bearer client) is not a browser's, carries no ambient cookie,
/// and is let through.
/// </para>
/// <para>
/// It runs in the main pipeline after authorization, so an unauthenticated upgrade is still answered
/// 401 exactly as an HTTP request with the same credential is, and before the hub's endpoint - and so
/// before the WebSocket is accepted. It is the hub's own check rather than
/// <c>WebSocketOptions.AllowedOrigins</c>, which cannot express "the request's own origin" and which the
/// test server's WebSocket client bypasses.
/// </para>
/// </remarks>
/// <param name="next">The next middleware in the pipeline.</param>
public sealed class NotificationHubOriginMiddleware(RequestDelegate next)
{
    /// <summary>
    /// Refuses a hub request from a foreign origin and passes everything else on.
    /// </summary>
    /// <param name="context">The request.</param>
    /// <param name="webSetting">The web app's configured domains.</param>
    public async Task InvokeAsync(HttpContext context, IOptions<WebSetting> webSetting)
    {
        if (context.Request.Path.StartsWithSegments(NotificationHub.Path, StringComparison.OrdinalIgnoreCase)
            && !IsAllowedOrigin(context.Request, webSetting.Value))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        await next(context);
    }

    /// <summary>
    /// Whether a hub request's <c>Origin</c> may open the hub: absent, the request's own origin, or one
    /// of the web app's domains. Origins are compared case-insensitively and without a trailing slash.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="webSetting">The web app's configured domains.</param>
    /// <returns><see langword="true"/> when the origin is allowed.</returns>
    public static bool IsAllowedOrigin(HttpRequest request, WebSetting webSetting)
    {
        var origin = request.Headers[HeaderNames.Origin].ToString();
        if (string.IsNullOrEmpty(origin))
        {
            return true;
        }

        var normalized = Normalize(origin);
        var own = Normalize($"{request.Scheme}://{request.Host.Value}");

        return string.Equals(normalized, own, StringComparison.OrdinalIgnoreCase)
               || webSetting.AllowedDomains().Any(domain => string.Equals(Normalize(domain), normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static string Normalize(string origin) => origin.Trim().TrimEnd('/');
}
