namespace Backend.Processors;

using Npgsql;

/// <summary>
/// Global FastEndpoints post-processor that converts unhandled exceptions thrown
/// during request processing into standardized JSON error responses. Special-cases
/// <see cref="DbUpdateException"/> to surface PostgreSQL constraint violations as
/// 400-level errors and treats any other exception as a 500 internal server error.
/// </summary>
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

        if (context.ExceptionDispatchInfo.SourceException.GetType() == typeof(DbUpdateException))
        {
            context.MarkExceptionAsHandled(); //only if handling the exception here.

            var ex = (DbUpdateException)context.ExceptionDispatchInfo.SourceException;
            logger.LogWarning(ex, "Database update failed for {Method} {Path}", context.HttpContext.Request.Method, context.HttpContext.Request.Path);
            context.HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.HttpContext.Response.ContentType = "application/json";
            var error = ex.InnerException is PostgresException pgEx ? GetErrorMessage(pgEx) : (null, ex.Message, ErrorCodes.DatabaseError);
            var response = new
            {
                type = "https://www.rfc-editor.org/rfc/rfc7231#section-6.5.1",
                title = "Db Update Failed",
                status = 400,
                instance = context.HttpContext.Request.Path.Value,
                traceId = context.HttpContext.TraceIdentifier,
                errors = new[] { new { name = error.propertyName, reason = error.message, error.code } }
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
                        reason = featureDisabled.Message,
                        code = ErrorCodes.FeatureDisabled
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
                        reason = limitExceeded.Message,
                        code = ErrorCodes.FeatureLimitExceeded
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
                        reason = env.IsDevelopment() ? ex.Message : "An unexpected error occurred.",
                        code = ErrorCodes.InternalServerError,
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
    /// Maps a <see cref="PostgresException"/> to a user-friendly error tuple of
    /// property name, message, and application error code based on the SQLSTATE
    /// (unique violation, not null violation, foreign key violation, check violation,
    /// or generic database error).
    /// </summary>
    private static (string? propertyName, string message, string code) GetErrorMessage(PostgresException ex)
    {
        switch (ex.SqlState)
        {
            // Unique violation
            case "23505":
                var constraintName = ex.ConstraintName;
                if (string.IsNullOrEmpty(constraintName))
                    return (null, "A duplicate value was found.", ErrorCodes.DuplicateValue);

                // Extract property name from constraint name (e.g. "IX_Users_Username" -> "username")
                var property = constraintName.Split('_').Last().ToLowerInvariant();
                return (property, $"A {property} with this value already exists.", ErrorCodes.DuplicatePropertyValue);

            // Not null violation
            case "23502":
                var columnName = ex.ColumnName?.ToLowerInvariant();
                if (string.IsNullOrEmpty(columnName))
                    return (null, "A required field is missing.", ErrorCodes.RequiredFieldMissing);

                return (columnName, $"The {columnName} field is required.", ErrorCodes.RequiredPropertyFieldMissing);

            // Foreign key violation
            case "23503":
                return (null, "Referenced record does not exist.", ErrorCodes.ReferencedRecordNotFound);

            // Check violation
            case "23514":
                return (null, "Invalid value provided.", ErrorCodes.InvalidValueProvided);

            default:
                return (null, "A database error occurred.", ErrorCodes.DatabaseError);
        }
    }
}
