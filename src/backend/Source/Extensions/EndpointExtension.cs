namespace Backend.Extensions;

using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using FluentValidation.Results;

/// <summary>
/// Helper extensions for FastEndpoints that allow endpoints to raise a validation failure carrying a
/// property name and a machine-readable <see cref="ErrorCode"/> in a single call.
/// </summary>
/// <remarks>
/// Called as <c>this.ThrowError(...)</c>. FastEndpoints' own <c>Endpoint&lt;TRequest,TResponse&gt;</c>
/// declares instance overloads named <c>ThrowError</c>, none of which accepts an <see cref="ErrorCode"/>:
/// through the <c>this.</c> receiver the compiler finds no applicable instance overload and binds these
/// extensions instead. A bare <c>ThrowError(ErrorCodes.X)</c> is a simple-name call, which never
/// considers extension methods, so it fails to compile - a missing receiver is a build error, never a
/// silent bind to FastEndpoints' message-only overload.
/// <para>
/// A call site names only the code - never an English message, which would drift from what
/// <c>error.server.&lt;code&gt;</c> actually ships and would have to be maintained in eight languages
/// besides. The <c>ValidationFailure.Message</c> these overloads set is filled in from
/// <see cref="ErrorLocalization.ResolveEnglishFallback"/> - the shipped English text for the code, with
/// no database involved - so it reads sensibly even for the one caller that never reaches the
/// localization pass FastEndpoints' <c>GlobalResponseModifierAsync</c> runs afterwards: a unit test
/// talking to a service directly, or a response built ahead of that pass.
/// </para>
/// </remarks>
public static class EndpointExtension
{
    /// <summary>
    /// Adds a validation failure to the endpoint and throws a
    /// <see cref="ValidationFailureException"/> to short-circuit the request.
    /// </summary>
    /// <param name="endpoint">The FastEndpoints endpoint raising the error.</param>
    /// <param name="propertyName">Name of the offending property, or empty for a request-level error.</param>
    /// <param name="errorCode">Machine-readable error code from <see cref="ErrorHandling.ErrorCodes"/>.</param>
    [DoesNotReturn]
    public static void ThrowError<TRequest, TResponse>(this Endpoint<TRequest, TResponse> endpoint, string propertyName, ErrorCode errorCode)
        where TRequest : notnull
    {
        var validationFailure = new ValidationFailure(propertyName, endpoint.HttpContext.ResolveEnglishFallback(errorCode))
        {
            ErrorCode = errorCode.Value
        };

        endpoint.ValidationFailures.Add(validationFailure);

        throw new ValidationFailureException(endpoint.ValidationFailures, $"{nameof(ThrowError)}() called!");
    }

    /// <summary>
    /// Raises a request-level (non-property-specific) validation error on the endpoint, under the
    /// general-errors field name FastEndpoints gives such errors (<c>generalErrors</c> by default), so
    /// clients reading an error's <c>name</c> see the same value FastEndpoints' own overloads produce.
    /// </summary>
    /// <param name="endpoint">The FastEndpoints endpoint raising the error.</param>
    /// <param name="errorCode">Machine-readable error code from <see cref="ErrorHandling.ErrorCodes"/>.</param>
    [DoesNotReturn]
    public static void ThrowError<TRequest, TResponse>(this Endpoint<TRequest, TResponse> endpoint, ErrorCode errorCode)
        where TRequest : notnull
    {
        // FastEndpoints' own AddError files a request-level failure under its configured general-errors
        // field name, which is what keeps that name in step with FastEndpoints' configuration.
        endpoint.AddError(endpoint.HttpContext.ResolveEnglishFallback(errorCode), errorCode.Value);

        throw new ValidationFailureException(endpoint.ValidationFailures, $"{nameof(ThrowError)}() called!");
    }

    /// <summary>
    /// Raises a property-scoped validation error, deriving the property name
    /// from the supplied expression.
    /// </summary>
    /// <param name="endpoint">The FastEndpoints endpoint raising the error.</param>
    /// <param name="property">Expression selecting the offending property on the request.</param>
    /// <param name="errorCode">Machine-readable error code from <see cref="ErrorHandling.ErrorCodes"/>.</param>
    [DoesNotReturn]
    public static void ThrowError<TRequest, TResponse>(this Endpoint<TRequest, TResponse> endpoint, Expression<Func<TRequest, object?>> property, ErrorCode errorCode)
        where TRequest : notnull
    {
        var propertyName = property.Body.GetPropertyChain();
        endpoint.ThrowError(propertyName, errorCode);
    }
}
