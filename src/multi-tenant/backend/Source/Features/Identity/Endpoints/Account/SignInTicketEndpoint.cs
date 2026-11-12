using Backend.Features.Tenancy.Core;
using Backend.Features.Tenancy.Core.Hosting;
using Backend.Features.Identity.Core;
using Backend.Features.Identity.Core.Sessions;

namespace Backend.Features.Identity.Endpoints.Account;

/// <summary>
/// Authenticated POST endpoint that issues a single-use sign-in ticket for one of the caller's tenants,
/// so the web app can open that tenant's own address already signed in.
/// </summary>
/// <remarks>
/// Each host keeps its own session, so a tenant's address knows nothing of the session the caller holds
/// here. The ticket is what crosses over: <c>POST /account/sign-in-ticket/redeem</c> on the tenant's host
/// exchanges it for a session of that host's own. It is issued on exactly the standing a sign-in there
/// would be authorized on - the tenant exists, the caller holds a membership of it and it is not
/// suspended - and declares no permission, since which tenants a caller may enter is decided by
/// memberships, not by roles. It carries the security stamp of the calling session, so the redemption
/// refuses it once the account's credentials have changed. The ticket lives for a minute, is redeemable
/// once, and only into this tenant. It is a bearer value: the web app never puts it in a URL, but hands it
/// to the tab it opened with <c>postMessage</c>, which accepts it only from the deployment's own origins,
/// so a ticket cannot be planted in a link that would sign a victim in to the sender's account.
/// </remarks>
sealed class SignInTicketEndpoint(AppDbContext dbContext,
                                  ICurrentUserService currentUserService,
                                  ITenantHostResolver tenantHostResolver,
                                  ISessionStore sessionStore,
                                  ISignInTicketStore signInTicketStore) : Endpoint<SignInTicketRequest, SignInTicketResponse>
{
    public override void Configure()
    {
        Post("sign-in-ticket");
        Group<AccountGroup>();
    }

    public override async Task HandleAsync(SignInTicketRequest req, CancellationToken c)
    {
        if (currentUserService.GetCurrentUserId() is not { } userId)
        {
            await Send.UnauthorizedAsync(c);
            return;
        }

        // Read across every tenant rather than through ITenantService, which narrows to the tenants the
        // caller may see and would report a tenant the caller merely does not belong to as absent.
        var tenant = await dbContext.Tenants
            .AsNoTracking()
            .AcrossAllTenants()
            .Where(x => x.Id == req.TenantId)
            .Select(x => new { x.Id, x.IdentifierNormalized, x.Status })
            .FirstOrDefaultAsync(c);
        if (tenant == null)
            this.ThrowError(ErrorCodes.TenantNotFound);

        var holdsMembership = await dbContext.TenantMemberships
            .AsNoTracking()
            .AcrossAllTenants()
            .AnyAsync(membership => membership.TenantId == tenant.Id && membership.UserId == userId, c);
        if (!holdsMembership)
            this.ThrowError(ErrorCodes.NotTenantMember);

        if (tenant.Status == TenantStatus.Suspended)
            this.ThrowError(ErrorCodes.TenantSuspended);

        // With host resolution off there is no address to open, so there is nothing to carry a ticket to.
        var tenantUrl = tenantHostResolver.TenantUrl(tenant.IdentifierNormalized);
        if (tenantUrl == null)
            this.ThrowError(ErrorCodes.TenantAddressNotConfigured);

        // The ticket carries the stamp the calling session was created under, never the account's current
        // one, so a session that outlived a credential change (a revocation cut short by a store outage)
        // cannot mint a fresh session under the new stamp: the redemption compares it with the account's.
        var sessionId = SessionClaims.ReadSessionId(User);
        var session = sessionId is null ? null : await sessionStore.GetAsync(sessionId, c);
        if (session is null || session.UserId != userId)
        {
            await Send.UnauthorizedAsync(c);
            return;
        }

        var ticket = await signInTicketStore.IssueAsync(new SignInTicket(userId, tenant.Id, session.SecurityStamp), c);

        Response = new SignInTicketResponse
        {
            Ticket = ticket,
            TenantUrl = tenantUrl,
        };
    }
}

sealed class SignInTicketRequest
{
    public Guid TenantId { get; set; }
}

sealed class SignInTicketRequestValidator : Validator<SignInTicketRequest>
{
    public SignInTicketRequestValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
    }
}

sealed class SignInTicketResponse
{
    /// <summary>The single-use ticket, handed to the tenant's host and to nothing else.</summary>
    public string Ticket { get; set; } = null!;

    /// <summary>The tenant's own address, where the ticket is redeemed.</summary>
    public string TenantUrl { get; set; } = null!;
}
