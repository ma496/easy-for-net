namespace Backend.Middleware;

using Backend.Features.Identity.Core;
using Backend.Tenancy;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

/// <summary>
/// Gives a reason to the refusal endpoint authorization produced, in the same error response every
/// other refusal in the application is reported through.
/// </summary>
/// <remarks>
/// <para>
/// Authorization refuses three different requests with an answer that does not say which it was: a
/// caller that is not signed in, a caller that is signed in but whose tenant selection is unusable,
/// and a caller that is signed in and acting in a healthy tenant but does not hold the permission the
/// endpoint requires. Only the first is about who the caller is. The other two are both reported as a
/// bare 403 with nothing to say what to do about it, and the web application has to tell them apart -
/// one is a permission the caller lacks, the other is a tenant they must choose a different one of
/// (AC-045, AC-070, AC-136).
/// </para>
/// <para>
/// Endpoint authorization is evaluated before any endpoint work, and therefore before the tenant
/// enforcement point, against the permissions the request currently holds. For a caller whose tenant
/// selection has gone stale - the tenant suspended or deleted, the membership removed - the session
/// check recomputes those permissions against no tenant, so they are none, so the first
/// permission-gated endpoint the caller calls is refused by authorization and the tenant enforcement
/// point never runs. That refusal is correct in outcome and wrong in explanation: it tells the caller
/// they lack a permission when what actually happened is that the tenant they were working in is no
/// longer usable, which is precisely what AC-023 and AC-050 require them to be told and what the web
/// app needs in order to offer them another tenant.
/// </para>
/// <para>
/// So this sits at the one seam that can see all three refusals and answers each with the code that
/// says which it was. It reports the tenant rule when the request's tenant is genuinely what refused
/// it: an authenticated caller, an endpoint that is not exempt from the tenant requirement, and a
/// selection that is not active. It reports the permission rule otherwise - a caller acting in a
/// healthy tenant, and the endpoints that establish a tenant, which declare no permission and are
/// refused for want of authority like any other. A challenge, being a caller with no account at all,
/// is answered with the code that says so. Every other authorization outcome is left to the
/// framework's own handler untouched.
/// </para>
/// </remarks>
public sealed class TenantRefusalResultHandler : IAuthorizationMiddlewareResultHandler
{
    /// <summary>
    /// The refusal reported for a caller that is signed in, is acting in a tenant it can use, and does
    /// not hold the permission the endpoint it called declares.
    /// </summary>
    private const string PermissionDeniedMessage =
        "The roles you hold in the tenant you are acting in do not grant the permission this operation requires.";

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
        // from, and nothing to do with tenants or permissions. The code is what makes it the defined
        // refusal self-service onboarding requires rather than a blank one (AC-136); the stored-file
        // endpoints ask for the standard unauthenticated response and get the same one this application
        // answers every unauthenticated request with (AC-098).
        if (authorizeResult.Challenged)
        {
            return (StatusCodes.Status401Unauthorized, AuthenticationRequiredMessage, ErrorCodes.AuthenticationRequired);
        }

        // A refusal reached by a caller with no account is not one this handler can explain: there is no
        // session that could name a tenant, and no permission the caller was supposed to hold. It is
        // left to the framework, which answers it as the challenge it is.
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        // An endpoint exempt from the tenant requirement is never refused for want of a tenant: it runs
        // in the platform scope for exactly the callers whose tenant is unusable, which is how such a
        // caller reaches the surfaces that give them one. Whatever it refused, it refused as a
        // permission.
        if (!IsExemptFromTenantRequirement(context))
        {
            var status = TenantSessionState.Read(context);

            if (status != TenantSessionStatus.Active)
            {
                var (message, errorCode) = TenantRefusal.Describe(status);

                return (StatusCodes.Status403Forbidden, message, errorCode);
            }
        }

        // Signed in, acting in a tenant it can use, and still refused: the caller does not hold what the
        // endpoint declared. Saying so is the error-code half of AC-045.
        return (StatusCodes.Status403Forbidden, PermissionDeniedMessage, ErrorCodes.PermissionDenied);
    }

    /// <summary>
    /// Reports whether the endpoint being called is exempt from the active tenant requirement, by
    /// reading <see cref="AllowNoTenantAttribute"/> off the endpoint's own type. A request whose
    /// endpoint cannot be identified is treated as not exempt, so an operation whose tenant rule cannot
    /// be established is refused rather than allowed to run unrestricted.
    /// </summary>
    /// <param name="context">The request being handled.</param>
    /// <returns><see langword="true"/> when the endpoint runs with no tenant established.</returns>
    private static bool IsExemptFromTenantRequirement(HttpContext context)
    {
        var definition = context.GetEndpoint()?.Metadata.GetMetadata<EndpointDefinition>();

        return definition is not null
               && definition.EndpointType.IsDefined(typeof(AllowNoTenantAttribute), inherit: false);
    }
}