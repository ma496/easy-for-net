namespace Backend.Features.Tenancy.Endpoints.Tenants;

using Backend.Features.Identity.Core;
using Backend.Features.Tenancy.Core;

/// <summary>
/// This endpoint that handles <c>POST /tenants/exit</c> to put a platform account's session back into
/// platform scope, leaving it authenticated and asking for no credentials.
/// </summary>
/// <remarks>
/// It is the counterpart of <c>POST /tenants/switch</c>, and exists because entering a tenant would
/// otherwise be one-way: a platform account holds no membership anywhere, so once its session names a
/// tenant there is no other tenant for it to select its way out through, and it would be left inside
/// that one until it signed in again.
/// <para>
/// Only a platform account may ask, and the tier is the test rather than a permission: inside a
/// tenant the session carries the tenant tier alone, so a platform-scoped permission would have been
/// narrowed away by the very act of entering and nothing would ever be able to leave. For anyone else
/// acting in no tenant is not a place to work but a state to leave: an account with memberships would
/// simply have every tenant-scoped request refused until it selected one again, so the surface is
/// closed to it rather than offered and then regretted.
/// </para>
/// <para>
/// Usable with no tenant established, because it is one of the places a tenant is
/// established - here by establishing none. Asking for one already active would be redundant, and
/// calling it while already in platform scope is deliberately not an error: the session is reissued
/// naming no tenant either way, which is the state the caller asked for.
/// </para>
/// </remarks>
sealed class TenantExitEndpoint(ITenantAuthorizationService tenantAuthorizationService,
                                ICurrentUserService currentUserService) : EndpointWithoutRequest<TenantExitResponse>
{
    public override void Configure()
    {
        Post("exit");
        Group<TenantsGroup>();
    }

    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var callerId = currentUserService.GetCurrentUserId();
        if (callerId is not { } userId)
        {
            await Send.UnauthorizedAsync(cancellationToken);
            return;
        }

        if (!currentUserService.IsPlatform())
        {
            await Send.ForbiddenAsync(cancellationToken);
            return;
        }

        // Re-establishes the session rather than editing the one in hand: the cookie principal is
        // re-signed without a tenant claim, a fresh access/refresh pair is issued and "no tenant" is
        // recorded on the refresh-token row, so neither this session nor a later refresh of it can act
        // in the tenant just left.
        var session = await tenantAuthorizationService.ReissueSessionAsync(userId, null, cancellationToken);

        await Send.ResponseAsync(new TenantExitResponse { Session = session }, cancellation: cancellationToken);
    }
}

/// <summary>
/// Response payload carrying the session material for a session that now acts in no tenant. A client
/// authenticating with tokens replaces its pair with the <see cref="TenantSessionDto"/>; a
/// cookie-authenticated client ignores it, because the re-signed cookie already names no tenant.
/// </summary>
public sealed class TenantExitResponse
{
    public TenantSessionDto Session { get; set; } = null!;
}
