namespace Backend.ErrorHandling;

/// <summary>
/// A machine-readable error code returned to API clients as a problem-details error's <c>code</c>,
/// and the key ("error.server.&lt;code&gt;") its message is localized from. <see cref="ErrorCodes"/>
/// declares one of these for every business error the API answers with.
/// </summary>
/// <remarks>
/// Deliberately carries no implicit conversion to <see cref="string"/>. FastEndpoints'
/// <c>Endpoint&lt;TRequest,TResponse&gt;</c> declares an instance <c>ThrowError(string message, int?
/// statusCode = null)</c>, and an applicable instance method always wins over an extension: with an
/// implicit conversion, <c>this.ThrowError(ErrorCodes.X)</c> would bind to that overload, sending the
/// code as the message and dropping it as a code. Without one, only the coded overloads on
/// <see cref="Backend.Extensions.EndpointExtension"/> accept it. Read <see cref="Value"/> explicitly
/// wherever a plain string is actually required: a <see langword="switch"/> case, an attribute argument,
/// a dictionary key, FluentValidation's own <c>WithErrorCode(string)</c>.
/// </remarks>
/// <param name="Value">The stable string sent to clients and used as the resource key's suffix.</param>
public readonly record struct ErrorCode(string Value)
{
    /// <inheritdoc cref="Value"/>
    public override string ToString() => Value;
}
