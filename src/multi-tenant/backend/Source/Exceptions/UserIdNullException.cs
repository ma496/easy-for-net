namespace Backend.Exceptions;

/// <summary>
/// Exception thrown when the current user's identifier is unexpectedly null,
/// typically because the request is made outside of an authenticated context.
/// </summary>
public class UserIdNullException(string message = "User ID is null.")
    : Exception(message)
{
}
