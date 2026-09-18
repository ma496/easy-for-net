namespace Backend.Features.Tenancy.Endpoints.Tenants;

using Backend.Features.Identity.Core;

/// <summary>
/// This endpoint that handles <c>POST /tenants/exit</c> to put a platform administrator's session
/// back into platform scope, leaving them authenticated and asking for no credentials.
/// </summary>
/// <remarks>
/// It is the counterpart of <c>POST /tenants/switch</c>, and exists because entering a tenant would
/// otherwise be one-way: a platform administrator holds no membership anywhere, so once their session
/// names a tenant there is no other tenant for them to select their way out through, and they would be
/// left inside it until they signed in again.
/// <para>
/// Only platform administration may ask. For anyone else acting in no tenant is not a place to work
/// but a state to leave: an account with memberships would simply have every tenant-scoped request
/// refused until it selected one again, so the surface is closed to it rather than offered and then
/// regretted.
/// </para>
/// <para>
/// Marked <see cref="AllowNoTenantAttribute"/> because it is one of the places a tenant is
/// established - here by establishing none. Asking for one already active would be redundant, and
/// calling it while already in platform scope is deliberately not an error: the session is reissued
/// naming no tenant either way, which is the state the caller asked for.
/// </para>
/// </remarks>
[AllowNoTenant]
sealed class TenantExitEndpoint(ITenantAuthorizationService tenantAuthorizationService,
                                ICurrentUserService currentUserService) : EndpointWithoutRequest<TenantExitResponse>
{
    public override void Configure()
    {
        Post("exit");
        Group<TenantsGroup>();
        Permissions(Allow.Platform_Administration);
    }

    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var callerId = currentUserService.GetCurrentUserId();
        if (callerId is not { } userId)
        {
            await Send.UnauthorizedAsync(cancellationToken);
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
