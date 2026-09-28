namespace Backend.Features.Identity.Endpoints.Account;

using Backend.Features.Identity.Core;

/// <summary>
/// Authenticated POST endpoint that signs the current user out by clearing the auth
/// cookie and the refresh-token cookie.
/// </summary>
/// <remarks>
/// Usable with no tenant established, because signing out is account self-service: a
/// caller acting in no tenant - an account that holds no usable membership, or one that has not
/// chosen between several - must still be able to end its session. Discarding the stored active
/// tenant selection together with the tenant-scoped data cached beside it is the web app's part of
/// the same flow, so the next account signing in on that browser inherits no selection and sees no
/// previous tenant's records.
/// </remarks>
sealed class SignoutEndpoint(ICurrentUserService currentUserService, IAuthTokenService authTokenService)
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
        await CookieAuth.SignOutAsync();
        HttpContext.Response.Cookies.Delete("refreshToken");
        await Send.OkAsync(c);
    }
}
