using Backend.Features.Tenancy.Core;
using Backend.Features.Tenancy.Core.Hosting;
using Backend.Features.Identity.Core;
using Backend.Features.Identity.Core.Sessions;
using Backend.Features.Settings.Core;
using Microsoft.Net.Http.Headers;

namespace Backend.Features.Identity.Endpoints.Account;

/// <summary>
/// Anonymous POST endpoint that exchanges a sign-in ticket from <c>POST /account/sign-in-ticket</c> for a
/// session on the tenant's own host, issuing the same token pair and cookies a password sign-in does.
/// </summary>
/// <remarks>
/// A browser request from another origin is refused with 403 before the ticket is read, so no other site
/// can plant its own ticket's session in a visitor's browser. The ticket is taken out of the store before
/// anything else is checked, so a ticket is spent by its
/// first redemption whatever the outcome. It names the one tenant it may enter, and on a resolved host
/// that tenant must be the host's: a ticket redeemed on the platform's address or another tenant's is
/// refused with <see cref="ErrorCodes.TenantHostMismatch"/>. Everything the issuance was authorized on is
/// read again, since up to a minute may have passed: the account must still be active, its security stamp
/// unchanged, its membership standing and the tenant not suspended, and the tenant's sign-in settings
/// still apply. A ticket that is unknown, spent, expired or no longer backed by that standing answers
/// <see cref="ErrorCodes.SignInTicketInvalid"/>, which tells the visitor to sign in with their password.
/// </remarks>
sealed class SignInTicketRedeemEndpoint(IUserService userService,
                                        AppDbContext dbContext,
                                        ITenantHostResolver tenantHostResolver,
                                        ISignInTicketStore signInTicketStore,
                                        ISessionIssuer sessionIssuer,
                                        ISettingProvider settingProvider) : Endpoint<SignInTicketRedeemRequest, TokenResponse>
{
    public override void Configure()
    {
        Post("sign-in-ticket/redeem");
        Group<AccountGroup>();
        AllowAnonymous();
    }

    public override async Task HandleAsync(SignInTicketRedeemRequest req, CancellationToken c)
    {
        // Only the tenant's own page redeems a ticket, so a browser request from any other origin - a
        // form another site posts, whose cookies the browser would keep - is refused before the ticket
        // is touched, rather than resting on the content type alone to keep it out.
        if (!IsSameOrigin(HttpContext.Request))
            this.ThrowError(ErrorCodes.PermissionDenied, StatusCodes.Status403Forbidden);

        var ticket = await signInTicketStore.TakeAsync(req.Ticket, c);
        if (ticket == null)
            this.ThrowError(ErrorCodes.SignInTicketInvalid);

        var tenant = await dbContext.Tenants
            .AsNoTracking()
            .AcrossAllTenants()
            .Where(x => x.Id == ticket.TenantId)
            .Select(x => new { x.Id, x.IdentifierNormalized, x.Status })
            .FirstOrDefaultAsync(c);
        if (tenant == null)
            this.ThrowError(ErrorCodes.SignInTicketInvalid);

        var host = tenantHostResolver.Resolve(HttpContext);
        if (host.IsResolved && (host.Kind != TenantHostKind.Tenant || !string.Equals(host.Identifier, tenant.IdentifierNormalized, StringComparison.OrdinalIgnoreCase)))
            this.ThrowError(ErrorCodes.TenantHostMismatch);

        var user = await userService.GetByIdAsync(ticket.UserId);
        if (user == null || !user.IsActive || user.SecurityStamp != ticket.SecurityStamp)
            this.ThrowError(ErrorCodes.SignInTicketInvalid);

        var holdsMembership = await dbContext.TenantMemberships
            .AsNoTracking()
            .AcrossAllTenants()
            .AnyAsync(membership => membership.TenantId == tenant.Id && membership.UserId == user.Id, c);
        if (!holdsMembership)
            this.ThrowError(ErrorCodes.SignInTicketInvalid);

        if (tenant.Status == TenantStatus.Suspended)
            this.ThrowError(ErrorCodes.TenantSuspended);

        var signinSettings = await settingProvider.GetAsync<SigninSettings>(tenant.Id, c);
        if (signinSettings.IsEmailVerificationRequired && !user.IsEmailVerified)
            this.ThrowError(ErrorCodes.EmailNotVerified);

        TokenService.RecordSessionTenant(HttpContext, tenant.Id);

        var session = await sessionIssuer.IssueAsync(user, tenant.Id, tenant.IdentifierNormalized, c);
        TokenService.RecordSessionId(HttpContext, session.SessionId, user.SecurityStamp);

        var claims = SessionClaims.ForToken(user.Id, session.SessionId);

        await CookieAuth.SignInAsync(u =>
        {
            u.Claims.AddRange(claims);
        });

        // The issuer persists the pair with the recorded tenant and session, and writes the refreshToken cookie.
        Response = await CreateTokenWith<TokenService>(user.Id.ToString(), u =>
        {
            u.Claims.AddRange(claims);
        });

        await userService.UpdateLastSigninAsync(user.Id);
    }

    /// <summary>
    /// Whether the request's <c>Origin</c> is absent (a non-browser client, which carries no ambient cookie
    /// to plant) or the origin the request itself was made to, its scheme and host compared
    /// case-insensitively. A browser sends <c>Origin</c> on every cross-origin POST, so another site's
    /// form or script always fails this.
    /// </summary>
    internal static bool IsSameOrigin(HttpRequest request)
    {
        var origin = request.Headers[HeaderNames.Origin].ToString();
        if (string.IsNullOrEmpty(origin))
            return true;

        return string.Equals(origin.Trim().TrimEnd('/'), $"{request.Scheme}://{request.Host.Value}", StringComparison.OrdinalIgnoreCase);
    }
}

sealed class SignInTicketRedeemRequest
{
    public string Ticket { get; set; } = null!;
}

sealed class SignInTicketRedeemRequestValidator : Validator<SignInTicketRedeemRequest>
{
    public SignInTicketRedeemRequestValidator()
    {
        RuleFor(x => x.Ticket).NotEmpty().MaximumLength(100);
    }
}
