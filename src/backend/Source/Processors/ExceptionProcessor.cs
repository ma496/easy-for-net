namespace Backend.Processors;

using Backend.Features.Localization.Core;
using Npgsql;

/// <summary>
/// Global FastEndpoints post-processor that converts unhandled exceptions thrown
/// during request processing into standardized JSON error responses. Special-cases
/// <see cref="DbUpdateException"/> to surface PostgreSQL constraint violations as
/// 400-level errors and treats any other exception as a 500 internal server error.
/// </summary>
/// <remarks>
/// The DB-error, feature-disabled, feature-limit-exceeded and generic 500 messages are all put through
/// <see cref="IErrorMessageLocalizer"/> before being written, the generic one included since production
/// still names it with its own <c>error.server.internalServerError</c> key rather than the exception's
/// own message. A global processor is built once, as a singleton, so <see cref="IErrorMessageLocalizer"/>
/// - scoped, because it reads the acting tenant and the database - is resolved from the request's own
/// <see cref="HttpContext.RequestServices"/> inside <see cref="PostProcessAsync"/> rather than taken as
/// a constructor dependency, which the root provider would refuse to construct at all.
/// </remarks>
public class ExceptionProcessor(IWebHostEnvironment env, ILogger<ExceptionProcessor> logger) : IGlobalPostProcessor
{
    /// <summary>
    /// Inspects the request context for an unhandled exception, maps it to an
    /// appropriate HTTP status code and standard error payload, and marks the
    /// exception as handled so the pipeline can short-circuit normally.
    /// </summary>
    public async Task PostProcessAsync(IPostProcessorContext context, CancellationToken ct)
    {
        if (!context.HasExceptionOccurred)
            return;

        var localizationService = context.HttpContext.RequestServices.GetRequiredService<IErrorMessageLocalizer>();

        if (context.ExceptionDispatchInfo.SourceException.GetType() == typeof(DbUpdateException))
        {
            context.MarkExceptionAsHandled(); //only if handling the exception here.

            var ex = (DbUpdateException)context.ExceptionDispatchInfo.SourceException;
            logger.LogWarning(ex, "Database update failed for {Method} {Path}", context.HttpContext.Request.Method, context.HttpContext.Request.Path);
            context.HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.HttpContext.Response.ContentType = "application/json";
            var error = ex.InnerException is PostgresException pgEx ? GetErrorMessage(pgEx) : (propertyName: null, code: ErrorCodes.DatabaseError);
            var fallback = context.HttpContext.ResolveEnglishFallback(error.code);
            var reason = await localizationService.LocalizeErrorAsync(context.HttpContext, error.code.Value, error.propertyName ?? "", fallback, ct);
            var response = new
            {
                type = "https://www.rfc-editor.org/rfc/rfc7231#section-6.5.1",
                title = "Db Update Failed",
                status = 400,
                instance = context.HttpContext.Request.Path.Value,
                traceId = context.HttpContext.TraceIdentifier,
                errors = new[] { new { name = error.propertyName, reason, code = error.code.Value } }
            };

            await context.HttpContext.Response.WriteAsJsonAsync(response, cancellationToken: ct);

            return;
        }

        // Placed before the catch-all below, which would otherwise report this as a 500. Entitlement
        // is not authorization: the refusal is identical for every caller in the tenant, its
        // administrator included, and it means the plan does not cover this rather than that the
        // caller lacks a grant - so it gets a 403 with its own code instead of being reported as a
        // permission failure or a fault.
        if (context.ExceptionDispatchInfo.SourceException is FeatureDisabledException featureDisabled)
        {
            context.MarkExceptionAsHandled();

            logger.LogInformation("Feature {Feature} is disabled for {Method} {Path}",
                                  featureDisabled.FeatureName,
                                  context.HttpContext.Request.Method,
                                  context.HttpContext.Request.Path);
            context.HttpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.HttpContext.Response.ContentType = "application/json";
            var featureDisabledFallback = context.HttpContext.ResolveEnglishFallback(ErrorCodes.FeatureDisabled);
            var featureDisabledReason = await localizationService.LocalizeErrorAsync(
                context.HttpContext, ErrorCodes.FeatureDisabled.Value, string.Empty, featureDisabledFallback, ct);
            var featureResponse = new
            {
                type = "https://www.rfc-editor.org/rfc/rfc7231#section-6.5.3",
                title = "Feature Disabled",
                status = 403,
                instance = context.HttpContext.Request.Path.Value,
                traceId = context.HttpContext.TraceIdentifier,
                errors = new[]
                {
                    new
                    {
                        name = featureDisabled.FeatureName,
                        reason = featureDisabledReason,
                        code = ErrorCodes.FeatureDisabled.Value
                    }
                }
            };

            await context.HttpContext.Response.WriteAsJsonAsync(featureResponse, cancellationToken: ct);

            return;
        }

        // A numeric limit is the same kind of answer as a disabled feature - the plan does not cover
        // this - so it is reported the same way, under its own code.
        if (context.ExceptionDispatchInfo.SourceException is FeatureLimitExceededException limitExceeded)
        {
            context.MarkExceptionAsHandled();

            logger.LogInformation("Feature limit {Feature} ({Limit}) reached for {Method} {Path}",
                                  limitExceeded.FeatureName,
                                  limitExceeded.Limit,
                                  context.HttpContext.Request.Method,
                                  context.HttpContext.Request.Path);
            context.HttpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.HttpContext.Response.ContentType = "application/json";
            var limitExceededFallback = context.HttpContext.ResolveEnglishFallback(ErrorCodes.FeatureLimitExceeded);
            var limitExceededReason = await localizationService.LocalizeErrorAsync(
                context.HttpContext, ErrorCodes.FeatureLimitExceeded.Value, string.Empty, limitExceededFallback, ct);
            var limitResponse = new
            {
                type = "https://www.rfc-editor.org/rfc/rfc7231#section-6.5.3",
                title = "Feature Limit Exceeded",
                status = 403,
                instance = context.HttpContext.Request.Path.Value,
                traceId = context.HttpContext.TraceIdentifier,
                errors = new[]
                {
                    new
                    {
                        name = limitExceeded.FeatureName,
                        reason = limitExceededReason,
                        code = ErrorCodes.FeatureLimitExceeded.Value
                    }
                }
            };

            await context.HttpContext.Response.WriteAsJsonAsync(limitResponse, cancellationToken: ct);

            return;
        }

        if (typeof(Exception).IsAssignableFrom(context.ExceptionDispatchInfo.SourceException.GetType()))
        {
            context.MarkExceptionAsHandled(); //only if handling the exception here.

            var ex = context.ExceptionDispatchInfo.SourceException;
            logger.LogError(ex, "Unhandled exception for {Method} {Path}", context.HttpContext.Request.Method, context.HttpContext.Request.Path);
            context.HttpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.HttpContext.Response.ContentType = "application/json";
            // Development keeps the exception's own message, which names the caller nothing they should
            // ever see in production - the generic message shown there is localized like every other
            // business error instead, so it too honours a tenant's own override.
            var internalServerErrorReason = env.IsDevelopment()
                ? ex.Message
                : await localizationService.LocalizeErrorAsync(
                    context.HttpContext, ErrorCodes.InternalServerError.Value, string.Empty,
                    context.HttpContext.ResolveEnglishFallback(ErrorCodes.InternalServerError), ct);
            var response = new
            {
                type = "https://www.rfc-editor.org/rfc/rfc7231#section-6.6.1",
                title = "Internal Server Error",
                status = 500,
                instance = context.HttpContext.Request.Path.Value,
                traceId = context.HttpContext.TraceIdentifier,
                errors = new[]
                {
                    new
                    {
                        reason = internalServerErrorReason,
                        code = ErrorCodes.InternalServerError.Value,
                        stackTrace = env.IsDevelopment() ? ex.StackTrace : null
                    }
                }
            };

            await context.HttpContext.Response.WriteAsJsonAsync(response, cancellationToken: ct);

            return;
        }

        context.ExceptionDispatchInfo.Throw();
    }

    /// <summary>
    /// Maps a <see cref="PostgresException"/> to a user-friendly error tuple of property name and
    /// application error code based on the SQLSTATE (unique violation, not null violation, foreign key
    /// violation, check violation, or generic database error). The message is never built here: it is
    /// resolved from the code, in the caller's own culture, by the localization pass every other coded
    /// error goes through.
    /// </summary>
    private static (string? propertyName, ErrorCode code) GetErrorMessage(PostgresException ex)
    {
        switch (ex.SqlState)
        {
            // Unique violation
            case "23505":
                var constraintName = ex.ConstraintName;
                if (string.IsNullOrEmpty(constraintName))
                    return (null, ErrorCodes.DuplicateValue);

                // Extract property name from constraint name (e.g. "IX_Users_Username" -> "username")
                var property = constraintName.Split('_').Last().ToLowerInvariant();
                return (property, ErrorCodes.DuplicatePropertyValue);

            // Not null violation
            case "23502":
                var columnName = ex.ColumnName?.ToLowerInvariant();
                if (string.IsNullOrEmpty(columnName))
                    return (null, ErrorCodes.RequiredFieldMissing);

                return (columnName, ErrorCodes.RequiredPropertyFieldMissing);

            // Foreign key violation
            case "23503":
                return (null, ErrorCodes.ReferencedRecordNotFound);

            // Check violation
            case "23514":
                return (null, ErrorCodes.InvalidValueProvided);

            default:
                return (null, ErrorCodes.DatabaseError);
        }
    }
}
