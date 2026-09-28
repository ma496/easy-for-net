namespace Backend.ErrorHandling;

using Backend.Features.Localization.Core;
using Backend.Processors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;

/// <summary>
/// The one place a coded error's message is put into the culture the request was served in - every
/// site that builds an error response (the problem-details response FastEndpoints sends for a
/// <c>ThrowError</c> call or a validator rule declared with
/// <c>.WithErrorCode(ErrorCodes.X.Value)</c>, and the hand-built responses <c>ExceptionProcessor</c> and
/// <c>ToLargePayloadProcessor</c> write directly) calls through here rather than repeating the
/// resolution itself. A code with no <c>error.server.&lt;code&gt;</c> key - a plain FluentValidation
/// rule (<c>NotEmptyValidator</c>, …) included - is left exactly as its caller wrote it.
/// </summary>
public static class ErrorLocalization
{
    /// <summary>
    /// The culture <see cref="Microsoft.AspNetCore.Builder.RequestLocalizationOptions"/> resolved for
    /// this request from its <c>Accept-Language</c> header, or English when the request localization
    /// middleware never ran for it (a call into the pipeline that bypasses <c>Program.cs</c>, as a unit
    /// test talking to a service directly would).
    /// </summary>
    public static string ResolveCulture(this HttpContext httpContext)
        => httpContext.Features.Get<IRequestCultureFeature>()?.RequestCulture.UICulture.Name ?? "en";

    /// <summary>
    /// The shipped English text for <paramref name="code"/> - <c>error.server.&lt;code&gt;</c> read
    /// straight off <see cref="ILocalizationResourceStore"/>, with no database involved - or
    /// <paramref name="code"/>'s own value when no such key is shipped. This is the one place a
    /// coded error's message is put into words with nothing but the process's own embedded resources:
    /// <c>Backend.Extensions.EndpointExtension.ThrowError</c> calls it to give a freshly raised
    /// failure its starting message, and <see cref="LocalizeErrorAsync"/>
    /// calls it again as the last resort when even the database-backed resolution has failed.
    /// </summary>
    public static string ResolveEnglishFallback(this HttpContext httpContext, ErrorCode code)
        => httpContext.TryResolveEnglish(code.Value) ?? code.Value;

    /// <summary>
    /// The shipped English <c>error.server.&lt;code&gt;</c> text for <paramref name="code"/>, or
    /// <see langword="null"/> when no such key is shipped - a plain FluentValidation rule code
    /// (<c>NotEmptyValidator</c>) among them.
    /// </summary>
    private static string? TryResolveEnglish(this HttpContext httpContext, string code)
    {
        var resourceStore = httpContext.RequestServices.GetRequiredService<ILocalizationResourceStore>();

        return resourceStore.EnglishResources.TryGetValue($"error.server.{code}", out var message) ? message : null;
    }

    /// <summary>
    /// Localizes <paramref name="fallback"/> for <paramref name="code"/> and <paramref name="propertyName"/>
    /// against the culture <paramref name="httpContext"/> was served in, or returns
    /// <paramref name="fallback"/> unchanged when <paramref name="code"/> is empty or carries no
    /// <c>error.server.&lt;code&gt;</c> key at all.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Passes <see cref="TenantContextProcessor.ReadSessionTenantId"/>'s reading of the request's own
    /// <c>tenant_id</c> claim through as the fallback tenant, so a response built ahead of
    /// <see cref="TenantContextProcessor"/> - the permission refusal
    /// <see cref="Backend.Middleware.AuthorizationRefusalResultHandler"/> answers with, in particular -
    /// still resolves the caller's own tenant overrides rather than only the platform's. It changes
    /// nothing about what the request may do: <see cref="IErrorMessageLocalizer"/> reads it for nothing
    /// but choosing override text, and only when no tenant scope was established at all.
    /// </para>
    /// <para>
    /// A failure resolving the message (the database being unreachable, say) is logged and answered
    /// with the shipped English text for <paramref name="code"/>, read from nothing but the process's
    /// own embedded resources, or with <paramref name="fallback"/> when <paramref name="code"/> has no
    /// resource key (a plain FluentValidation rule) - rather than allowed to turn an already-classified
    /// error into an unrelated 500. Request cancellation is left to propagate as it always does.
    /// </para>
    /// </remarks>
    public static async Task<string> LocalizeErrorAsync(
        this IErrorMessageLocalizer localizationService,
        HttpContext httpContext,
        string? code,
        string propertyName,
        string fallback,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(code))
        {
            return fallback;
        }

        try
        {
            // Read under the same condition TenantContextProcessor applies, so the two readings of the
            // session's tenant cannot diverge if another authentication scheme is added.
            var sessionTenantId = httpContext.User.Identity?.IsAuthenticated == true
                ? TenantContextProcessor.ReadSessionTenantId(httpContext.User)
                : null;
            var localized = await localizationService.TryLocalizeErrorAsync(
                httpContext.ResolveCulture(), code, propertyName, sessionTenantId, cancellationToken);

            return localized ?? fallback;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var logger = httpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(ErrorLocalization));
            logger.LogWarning(ex, "Failed to localize error code {Code}; serving its shipped English text instead.", code);

            return httpContext.TryResolveEnglish(code) ?? fallback;
        }
    }
}