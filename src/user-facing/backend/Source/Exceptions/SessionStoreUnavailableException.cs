namespace Backend.Exceptions;

/// <summary>
/// Exception thrown when the session store cannot be reached or does not answer. A session that cannot
/// be looked up is neither valid nor invalid, so the request is refused with a 503 rather than
/// treated as anonymous: <c>ExceptionProcessor</c> and the session-validation middleware answer it
/// with <c>sessionStoreUnavailable</c>.
/// </summary>
/// <param name="message">What failed.</param>
/// <param name="innerException">The store client's own failure.</param>
public sealed class SessionStoreUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException)
{
}
