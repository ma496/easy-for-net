namespace Backend.Features.Tenancy.Core;

using Backend.Features.Tenancy.Core.Entities;

/// <summary>
/// The membership questions other features ask, answered in identifiers alone so that no membership
/// row ever leaves this slice. Each member hands back an unexecuted query, so a caller composes the
/// answer into its own query and the database still settles the whole question in one round trip.
/// </summary>
/// <remarks>
/// This is deliberately separate from <see cref="ITenantMembershipService"/>, which administers
/// memberships and reaches the identity slice to do it. A read as ordinary as "who belongs here"
/// cannot be published through a service that depends on identity, because the identity services
/// that need the answer are the very ones that service depends on, and the container would refuse
/// the cycle. Nothing here depends on anything but the database, so any feature may take it.
/// <para>
/// Every read relaxes tenant restriction by name and states the tenant it means in its own
/// predicate: the caller may be acting in another tenant, or in none at all, while asking. The
/// soft-delete filter stays in force throughout, so a membership that was removed places nobody -
/// which is exactly what makes re-adding the same account to a tenant an ordinary creation.
/// </para>
/// </remarks>
[AllowOutside]
public interface ITenantMembershipQuery
{
    /// <summary>
    /// The accounts holding a live membership of one tenant.
    /// </summary>
    /// <param name="tenantId">
    /// The tenant whose members are being asked for, or <see langword="null"/> - which matches no
    /// membership at all. A caller acting in platform scope has no tenant to name, and an empty
    /// answer is the truthful one there: a tenant's members are not the platform's accounts.
    /// </param>
    /// <returns>A composable query over those accounts' identifiers.</returns>
    IQueryable<Guid> MemberUserIds(Guid? tenantId);

    /// <summary>
    /// The tenants one account holds a live membership of.
    /// </summary>
    /// <param name="userId">The account being asked about.</param>
    /// <returns>A composable query over those tenants' identifiers.</returns>
    IQueryable<Guid> MemberTenantIds(Guid userId);
}

/// <summary>
/// EF Core-backed implementation of <see cref="ITenantMembershipQuery"/>. It takes the database and
/// nothing else, which is what keeps it safe for any feature to depend on.
/// </summary>
[NoDirectUse]
public class TenantMembershipQuery(AppDbContext dbContext) : ITenantMembershipQuery
{
    /// <inheritdoc />
    public IQueryable<Guid> MemberUserIds(Guid? tenantId)
        => LiveMemberships()
            .Where(membership => membership.TenantId == tenantId)
            .Select(membership => membership.UserId);

    /// <inheritdoc />
    public IQueryable<Guid> MemberTenantIds(Guid userId)
        => LiveMemberships()
            .Where(membership => membership.UserId == userId && membership.TenantId != null)
            .Select(membership => membership.TenantId!.Value);

    /// <summary>
    /// Every live membership there is, with tenant restriction relaxed by name so that the callers
    /// above can state the tenant they mean themselves, and the soft-delete filter left in force so
    /// that a removed membership answers nothing.
    /// </summary>
    /// <returns>A composable query over the membership rows.</returns>
    private IQueryable<TenantMembership> LiveMemberships()
        => dbContext.TenantMemberships
            .AsNoTracking()
            .AcrossAllTenants();
}
