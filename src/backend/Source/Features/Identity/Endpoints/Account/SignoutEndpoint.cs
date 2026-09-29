namespace Backend.Features.Identity.Endpoints.Account;

using Backend.Features.Identity.Core;
using Backend.Features.Identity.Core.Sessions;

/// <summary>
/// Authenticated POST endpoint that signs the current user out by deleting the session it is
/// signed in with, revoking its refresh tokens and clearing the auth cookie and the refresh-token
/// cookie.
/// </summary>
/// <remarks>
/// Usable with no tenant established, because signing out is account self-service: a
/// caller acting in no tenant - an account that holds no usable membership, or one that has not
/// chosen between several - must still be able to end its session. Discarding the stored active
/// tenant selection together with the tenant-scoped data cached beside it is the web app's part of
/// the same flow, so the next account signing in on that browser inherits no selection and sees no
/// previous tenant's records.
/// </remarks>
sealed class SignoutEndpoint(ICurrentUserService currentUserService, IAuthTokenService authTokenService, ISessionStore sessionStore)
    : EndpointWithoutRequest<EmptyResponse>
{
    public override void Configure()
    {
        Post("signout");
        Group<AccountGroup>();
    }

    public override async Task HandleAsync(CancellationToken c)
    {
        var userId = currentUserService.GetCurrentUserId();
        if (userId.HasValue)
        {
            await authTokenService.RevokeAllAsync(userId.Value, c);
        }

        // The session the caller is signing out of is deleted too, not only the refresh tokens: the access
        // token it signs out with must stop working now, not when it would have expired.
        if (SessionClaims.ReadSessionId(User) is { } sessionId)
        {
            await sessionStore.DeleteAsync(sessionId, c);
        }

        await CookieAuth.SignOutAsync();
        HttpContext.Response.Cookies.Delete("refreshToken");
        await Send.OkAsync(c);
    }
}
