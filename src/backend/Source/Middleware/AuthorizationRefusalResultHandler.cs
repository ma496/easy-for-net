namespace Backend.Middleware;

using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

/// <summary>
/// Gives a reason to the refusal endpoint authorization produced, in the same error response every
/// other refusal in the application is reported through.
/// </summary>
/// <remarks>
/// <para>
/// Authorization refuses two different requests with an answer that does not say which it was: a
/// caller that is not signed in, and a caller that is signed in but does not hold the permission the
/// endpoint requires. The framework answers both with a bare status and no body, so the web
/// application is left with nothing to tell the person - one is an account they need, the other is a
/// grant somebody has to give them.
/// </para>
/// <para>
/// So this sits at the one seam that can see both and answers each with the code that says which it
/// was. Every other authorization outcome is left to the framework's own handler untouched.
/// </para>
/// </remarks>
public sealed class AuthorizationRefusalResultHandler : IAuthorizationMiddlewareResultHandler
{
    /// <summary>
    /// The refusal reported for a caller that is signed in and does not hold the permission the
    /// endpoint it called declares.
    /// </summary>
    private const string PermissionDeniedMessage =
        "The roles you hold do not grant the permission this operation requires.";

    /// <summary>
    /// The refusal reported for an operation that requires an account when the request carries none.
    /// </summary>
    private const string AuthenticationRequiredMessage =
        "This operation requires an account. Sign in and try again.";

    private readonly AuthorizationMiddlewareResultHandler _default = new();

    /// <summary>
    /// Answers the request with the refusal that names what actually refused it, and otherwise leaves
    /// the answer to the framework's own handler.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="context">The request being handled.</param>
    /// <param name="policy">The policy that was evaluated.</param>
    /// <param name="authorizeResult">The outcome of evaluating it.</param>
    public async Task HandleAsync(RequestDelegate next,
                                  HttpContext context,
                                  AuthorizationPolicy policy,
                                  PolicyAuthorizationResult authorizeResult)
    {
        if (DescribeRefusal(context, authorizeResult) is { } refusal)
        {
            await context.Response.SendErrorsAsync(
                [new ValidationFailure(string.Empty, refusal.Message) { ErrorCode = refusal.ErrorCode }],
                refusal.StatusCode,
                cancellation: context.RequestAborted);
            return;
        }

        await _default.HandleAsync(next, context, policy, authorizeResult);
    }

    /// <summary>
    /// The refusal to report, or <see langword="null"/> when this handler has no business answering the
    /// request and the framework's own answer should stand.
    /// </summary>
    /// <param name="context">The request being handled.</param>
    /// <param name="authorizeResult">The outcome of evaluating the policy.</param>
    /// <returns>
    /// The status, message and error code to refuse the request with, when authorization refused it for
    /// a reason this handler can name.
    /// </returns>
    private static (int StatusCode, string Message, string ErrorCode)? DescribeRefusal(HttpContext context,
                                                                                       PolicyAuthorizationResult authorizeResult)
    {
        // A policy that succeeded reached no refusal to explain.
        if (authorizeResult.Succeeded)
        {
            return null;
        }

        // A challenge is the caller not being authenticated at all: a 401 whatever endpoint it came
        // from. The code is what makes it a defined refusal rather than a blank one, and the
        // stored-file endpoints ask for the standard unauthenticated response and get the same one
        // this application answers every unauthenticated request with.
        if (authorizeResult.Challenged)
        {
            return (StatusCodes.Status401Unauthorized, AuthenticationRequiredMessage, ErrorCodes.AuthenticationRequired);
        }

        // A refusal reached by a caller with no account is not one this handler can explain: there is
        // no permission the caller was supposed to hold. It is left to the framework, which answers it
        // as the challenge it is.
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        // Signed in and still refused: the caller does not hold what the endpoint declared.
        return (StatusCodes.Status403Forbidden, PermissionDeniedMessage, ErrorCodes.PermissionDenied);
    }
}
