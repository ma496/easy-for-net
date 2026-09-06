namespace Backend.Features.Identity.Endpoints.Account;

using Backend.Features.Identity.Core;

/// <summary>
/// Authenticated POST endpoint that signs the current user out by clearing the auth
/// cookie and the refresh-token cookie.
/// </summary>
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
