namespace Backend.Features.Identity.Core;

using System.Security.Claims;
using System.Security.Cryptography;
using Backend.Data.Entities;

/// <summary>
/// Re-reads, on every request, everything the authorization of that request depends on: that the
/// session was issued for the account's current password and that the account is still active, that
/// the tenant the session names may still be acted in, and the roles and permissions the account
/// holds there as they stand right now. Nothing but the identifiers is taken from the session
/// itself, so a change made after the session was established - a membership removed, a tenant
/// suspended or deleted, a role assignment replaced, a role's permissions changed or the role
/// deleted - takes effect on the very next request, with no second sign-in and no wait for the
/// session to expire.
/// </summary>
public static class SessionValidator
{
    /// <summary>
    /// Evaluates a principal against current data and returns the authorization the request is
    /// actually entitled to. The caller replaces the principal's role and permission claims with the
    /// returned set, so that the claims an authorization policy sees are always the ones the database
    /// holds at request time rather than the ones minted when the session began.
    /// </summary>
    /// <param name="principal">The authenticated principal carried by the request.</param>
    /// <param name="dbContext">The database context used to read the account, the tenant and the grants.</param>
    /// <param name="cancellationToken">Token used to cancel the reads.</param>
    /// <returns>
    /// The state of the session: whether it survives at all, whether its tenant may be acted in, and
    /// the role and permission names it currently carries.
    /// </returns>
    public static async Task<SessionState> EvaluateAsync(ClaimsPrincipal? principal, AppDbContext dbContext, CancellationToken cancellationToken = default)
    {
        var userIdValue = principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var sessionVersion = principal?.FindFirstValue(ClaimConstants.SessionVersion);
        if (!Guid.TryParse(userIdValue, out var userId) || string.IsNullOrWhiteSpace(sessionVersion))
        {
            return SessionState.Expired;
        }

        Guid? tenantId = Guid.TryParse(principal?.FindFirstValue(ClaimConstants.TenantId), out var sessionTenantId)
            ? sessionTenantId
            : null;

        // The session check runs before any tenant scope is established for the request, so every
        // tenant-restricted set it reads relaxes tenant restriction by name: the tenant filter reads
        // a scope that is deliberately not resolved this early and would otherwise refuse the read.
        // The soft-delete filter stays in force throughout, which is what makes a deleted tenant read
        // as absent and a deleted role stop granting anything.
        var account = await dbContext.Users
            .AsNoTracking()
            .Where(candidate => candidate.Id == userId && candidate.IsActive)
            .Select(candidate => new
            {
                candidate.PasswordHash,
                Status = dbContext.Tenants
                    .Where(tenant => tenant.Id == tenantId)
                    .Select(tenant => (TenantStatus?)tenant.Status)
                    .FirstOrDefault(),
                HoldsMembership = dbContext.TenantMemberships
                    .AcrossAllTenants()
                    .Any(membership => membership.TenantId == tenantId && membership.UserId == userId)
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (account is null || !IssuedForCurrentPassword(sessionVersion, account.PasswordHash))
        {
            return SessionState.Expired;
        }

        // Order matters and mirrors the order the refusal is reported in: a tenant that is gone is
        // reported as absent rather than as one the caller was removed from, and a suspended tenant
        // is reported as suspended whether or not the membership survived the suspension.
        var tenantStatus = tenantId is null
            ? TenantSessionStatus.NoActiveTenant
            : account.Status switch
            {
                null => TenantSessionStatus.TenantNotFound,
                TenantStatus.Suspended => TenantSessionStatus.TenantSuspended,
                _ when !account.HoldsMembership => TenantSessionStatus.MembershipRevoked,
                _ => TenantSessionStatus.Active
            };

        // A selection that no longer names a tenant the account may act in confers nothing: the
        // grants below are recomputed as if no tenant were active, so the stale selection carries no
        // authority into the request and the caller is left to choose again.
        var actingTenantId = tenantStatus == TenantSessionStatus.Active ? tenantId : null;

        // The grants of the tenant being acted in, together with the account's platform-scoped roles,
        // which belong to no tenant and are therefore neither conferred nor withdrawn by one. With no
        // tenant being acted in only the platform-scoped roles remain, which for an ordinary account
        // is nothing at all.
        var grants = await dbContext.Roles
            .AsNoTracking()
            .AcrossAllTenants()
            .Where(role => (role.TenantId == null || role.TenantId == actingTenantId)
                           && dbContext.UserRoles.Any(assignment => assignment.UserId == userId && assignment.RoleId == role.Id))
            .Select(role => new
            {
                role.Name,
                Permissions = role.RolePermissions.Select(rolePermission => rolePermission.Permission.Name).ToList()
            })
            .ToListAsync(cancellationToken);

        return new SessionState
        {
            IsCurrent = true,
            TenantStatus = tenantStatus,
            Roles = [.. grants.Select(grant => grant.Name).Distinct(StringComparer.Ordinal)],
            Permissions = [.. grants.SelectMany(grant => grant.Permissions).Distinct(StringComparer.Ordinal)]
        };
    }

    /// <summary>
    /// Compares the version the session carries with the one the stored password hash produces now,
    /// in constant time, so that a password change ends every session established before it.
    /// </summary>
    /// <param name="sessionVersion">The version claim carried by the session.</param>
    /// <param name="passwordHash">The account's stored password hash.</param>
    /// <returns><see langword="true"/> when the session was issued for the current password.</returns>
    private static bool IssuedForCurrentPassword(string sessionVersion, string passwordHash)
    {
        try
        {
            return CryptographicOperations.FixedTimeEquals(
                Convert.FromBase64String(sessionVersion),
                Convert.FromBase64String(Helper.CreateSessionVersion(passwordHash)));
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

/// <summary>
/// What a session is entitled to on the request being handled. It is recomputed for every request,
/// so it describes the account, the tenant and the grants as they stand now, never as they stood
/// when the session was established.
/// </summary>
public sealed class SessionState
{
    /// <summary>
    /// A session that no longer belongs to a usable account - unknown, deactivated, or established
    /// before the account's most recent password change - and that therefore carries no
    /// authorization at all.
    /// </summary>
    public static SessionState Expired { get; } = new();

    /// <summary>
    /// Gets a value indicating whether the session still belongs to an active account and was issued
    /// for its current password. When it is <see langword="false"/> the session is over and the
    /// caller is signed out; every other member describes an authenticated caller.
    /// </summary>
    public bool IsCurrent { get; init; }

    /// <summary>
    /// Gets whether the tenant the session names may still be acted in, and when it may not, why.
    /// The caller stays authenticated in every case: a tenant that is gone, suspended or no longer
    /// joined is a refusal of tenant-scoped work with an explanation, never a sign-out.
    /// </summary>
    public TenantSessionStatus TenantStatus { get; init; } = TenantSessionStatus.NoActiveTenant;

    /// <summary>
    /// Gets the names of the roles the account holds right now: those granted inside the tenant being
    /// acted in, plus its platform-scoped roles. Empty while the session carries none.
    /// </summary>
    public IReadOnlyList<string> Roles { get; init; } = [];

    /// <summary>
    /// Gets the names of the permissions those roles grant right now, without duplicates.
    /// </summary>
    public IReadOnlyList<string> Permissions { get; init; } = [];
}

/// <summary>
/// Whether the tenant a session names may be acted in on this request, and when it may not, the
/// reason - which is what the single tenant enforcement point reports to the caller, so that a
/// refusal says which of the four things went wrong instead of only that something did.
/// </summary>
public enum TenantSessionStatus
{
    /// <summary>
    /// The session names no tenant: the account holds no active membership, or holds several and has
    /// not chosen between them. Tenant-scoped work is refused until one is selected.
    /// </summary>
    NoActiveTenant = 0,

    /// <summary>
    /// The session names a tenant that exists, is not suspended, and that the account still holds an
    /// active membership in. This is the only value that lets tenant-scoped work proceed.
    /// </summary>
    Active = 1,

    /// <summary>
    /// The session names a tenant that no longer exists or that has been deleted.
    /// </summary>
    TenantNotFound = 2,

    /// <summary>
    /// The session names a tenant that has been suspended.
    /// </summary>
    TenantSuspended = 3,

    /// <summary>
    /// The session names a live tenant whose membership the account no longer holds. The account
    /// stays authenticated and its access to every other tenant is untouched.
    /// </summary>
    MembershipRevoked = 4
}
