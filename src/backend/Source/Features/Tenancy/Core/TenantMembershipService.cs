namespace Backend.Features.Tenancy.Core;

using Backend.Data.Entities;
using Backend.Features.Identity.Core;

/// <summary>
/// Owns who belongs to a tenant: whether an account holds a membership there, and the three changes
/// that can be made to one - added, re-roled, removed. A membership carries no state beyond its own
/// existence, so every question below is answered by the row and never by a status column: an
/// account is a member of a tenant while a membership row for the pair exists and has not been
/// removed, and a removed membership is soft-deleted, which is what makes re-adding the same account
/// an ordinary creation rather than a special case.
/// </summary>
/// <remarks>
/// Every member names the tenant it acts for and none of them reads the active tenant scope, because
/// membership is administered both from inside a tenant and from the platform, and the two must not
/// behave differently.
/// <para>
/// The last-administrator guard lives here rather than in the endpoints, so that the one rule - a
/// tenant is never left with no member holding tenant administration - cannot be applied by one
/// write path and forgotten by another. Removal and role replacement need it asked at different
/// moments, and it is asked correctly for each here, once. Each write path also locks the tenant for
/// the duration of its transaction, because a guard that counts administrators has to count them
/// against a tenant nobody else is changing at the same moment.
/// </para>
/// <para>
/// A tenant's own lifecycle is not this service's question. An active membership is a live membership
/// row <c>and</c> a tenant that is neither suspended nor deleted, and the second half belongs to
/// <see cref="ITenantService"/>: a deleted tenant is not visible there at all, and a suspended one is
/// refused by the caller that must refuse it. That separation is deliberate - removing a member from
/// a suspended tenant is permitted, so a lookup here that quietly required the tenant to be active
/// would report a membership that exists as missing.
/// </para>
/// </remarks>
[AllowOutside]
public interface ITenantMembershipService
{
    /// <summary>
    /// The message reported when an account already belongs to the tenant it is being added to. The
    /// add surface raises it against its own user field with
    /// <see cref="ErrorCodes.DuplicateTenantMembership"/>.
    /// </summary>
    const string DuplicateMembershipMessage = "User is already a member of this tenant";

    /// <summary>
    /// The message reported when a change would leave a tenant with nobody able to administer it.
    /// Both the removal and the role-replacement surfaces raise it with
    /// <see cref="ErrorCodes.LastTenantAdministrator"/>, so the refusal reads the same whichever
    /// change provoked it.
    /// </summary>
    const string LastAdministratorMessage = "Tenant must keep at least one member holding tenant administration";

    /// <summary>
    /// Tells whether an account holds a membership of a tenant right now. A membership that was
    /// removed is soft-deleted and so is not one, which is what lets an account that once belonged to
    /// a tenant be added to it again without its earlier removal standing in the way.
    /// </summary>
    /// <param name="tenantId">The tenant being asked about.</param>
    /// <param name="userId">The account being asked about.</param>
    /// <param name="cancellationToken">Token used to cancel the read.</param>
    /// <returns><see langword="true"/> when the account is currently a member of that tenant.</returns>
    Task<bool> IsMemberAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The account's membership of a tenant, or <see langword="null"/> when it holds none - which an
    /// account that was removed from the tenant holds, exactly as one that never joined it does.
    /// </summary>
    /// <param name="tenantId">The tenant the membership belongs to.</param>
    /// <param name="userId">The account whose membership is read.</param>
    /// <param name="cancellationToken">Token used to cancel the read.</param>
    /// <returns>The membership, or <see langword="null"/> when there is none.</returns>
    /// <remarks>
    /// The row is tracked, so a caller may read the membership's identity and its per-tenant audit
    /// values from it - a membership's audit values are the tenant's own history of the account,
    /// distinct from the account's.
    /// </remarks>
    Task<TenantMembership?> GetAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Makes an account a member of a tenant and grants it exactly the roles named, and nothing else,
    /// inside that tenant. The account's memberships of other tenants, and the roles it holds there,
    /// lie outside everything this writes.
    /// </summary>
    /// <param name="tenantId">The tenant the account is joining.</param>
    /// <param name="userId">The account being made a member.</param>
    /// <param name="roleIds">The roles the new member is to hold in that tenant.</param>
    /// <param name="cancellationToken">Token used to cancel the reads and the writes.</param>
    /// <returns>The membership created, and the roles it was actually granted.</returns>
    /// <remarks>
    /// The caller checks first that the account exists, that it is not already a member - with
    /// <see cref="IsMemberAsync"/>, which is what keeps an earlier removed membership out of that
    /// comparison - and that every role named belongs to this tenant, and refuses the request itself,
    /// because each of those is a field-level validation failure on the request the caller owns.
    /// <para>
    /// A tenant that has no member yet gets one more role than was asked for: its system-created
    /// administrator role is added to the set, so that the first member of a tenant can always
    /// administer it and the last-administrator guard can never be violated at the moment a tenant
    /// gains its first member. That is why the granted roles are returned rather than assumed to be
    /// the ones supplied. For every later member the set is taken exactly as given.
    /// </para>
    /// </remarks>
    Task<TenantMembershipAddResult> AddAsync(Guid tenantId, Guid userId, IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces a member's roles inside one tenant with exactly the set supplied, unless doing so
    /// would leave the tenant with no member holding tenant administration, in which case nothing is
    /// written at all. The member's roles in every other tenant are untouched, so a change made in
    /// one tenant never alters what the same account may do in another.
    /// </summary>
    /// <param name="tenantId">The tenant whose assignments are being replaced.</param>
    /// <param name="userId">The member whose assignments are being replaced.</param>
    /// <param name="roleIds">The roles the member is to hold in that tenant, and no others.</param>
    /// <param name="cancellationToken">Token used to cancel the reads and the writes.</param>
    /// <returns>
    /// <see cref="TenantMembershipChangeOutcome.Applied"/> when the replacement was persisted,
    /// <see cref="TenantMembershipChangeOutcome.MembershipNotFound"/> when the account is not a member
    /// of the tenant, or <see cref="TenantMembershipChangeOutcome.LastTenantAdministrator"/> when the
    /// change was refused and rolled back.
    /// </returns>
    /// <remarks>
    /// The change reaches the member's live sessions on their next request, because what a session may
    /// do is recomputed from the assignments each time rather than read from the session itself; the
    /// member is neither signed out nor asked for credentials.
    /// <para>
    /// The membership row is written along with the assignments, so of two callers replacing the same
    /// member's roles at once the later one fails with <c>DbUpdateConcurrencyException</c> instead of
    /// silently overwriting a set it never saw. The caller reports that as
    /// <see cref="ErrorCodes.ConcurrentModification"/>.
    /// </para>
    /// </remarks>
    Task<TenantMembershipChangeOutcome> ReplaceRolesAsync(Guid tenantId, Guid userId, IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a member from a tenant: the membership is revoked and the roles the member held inside
    /// that tenant are withdrawn, unless the removal would leave the tenant with no member holding
    /// tenant administration, in which case nothing is written at all. The user account itself, its
    /// memberships of other tenants and the roles it holds there all survive untouched.
    /// </summary>
    /// <param name="tenantId">The tenant the account is being removed from.</param>
    /// <param name="userId">The member being removed.</param>
    /// <param name="cancellationToken">Token used to cancel the reads and the writes.</param>
    /// <returns>
    /// <see cref="TenantMembershipChangeOutcome.Applied"/> when the removal was persisted,
    /// <see cref="TenantMembershipChangeOutcome.MembershipNotFound"/> when the account is not a member
    /// of the tenant, or <see cref="TenantMembershipChangeOutcome.LastTenantAdministrator"/> when the
    /// removal was refused and nothing was written.
    /// </returns>
    /// <remarks>
    /// The removed member's existing sessions lose access to this tenant on their next request, and
    /// only to this tenant: no password is changed, no session is ended, and a session acting in
    /// another tenant the account still belongs to is unaffected. Nothing has to be pushed to those
    /// sessions for that to happen - what a session may do is recomputed from the membership and the
    /// assignments on every request, and this removes both.
    /// </remarks>
    Task<TenantMembershipChangeOutcome> RemoveAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// EF Core-backed implementation of <see cref="ITenantMembershipService"/>. Membership rows are
/// themselves tenant-scoped, so every read here relaxes tenant restriction by name and states the
/// tenant it means in its own predicate - the caller may be a platform administrator acting on a
/// tenant their session is not in, or acting in no tenant at all - while the soft-delete filter stays
/// in force throughout, which is what makes a removed membership keep nobody inside a tenant. The
/// role graph belongs to the identity slice and is reached only through the one contract that slice
/// publishes, so no account, role or assignment type is named here.
/// </summary>
[NoDirectUse]
public class TenantMembershipService(AppDbContext dbContext,
                                     ITenantAuthorizationService tenantAuthorizationService,
                                     ITenantContext tenantContext) : ITenantMembershipService
{
    /// <summary>
    /// Excludes nobody from the last-administrator count. Replacing a member's roles has to count
    /// that member too - the set being granted may itself be what keeps the tenant administered - so
    /// it asks with this rather than with the member's own identity.
    /// </summary>
    private static readonly Guid _everyMember = Guid.Empty;

    /// <inheritdoc />
    public Task<bool> IsMemberAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
        => Memberships(tenantId)
            .AsNoTracking()
            .AnyAsync(membership => membership.UserId == userId, cancellationToken);

    /// <inheritdoc />
    public Task<TenantMembership?> GetAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
        => Memberships(tenantId)
            .FirstOrDefaultAsync(membership => membership.UserId == userId, cancellationToken);

    /// <inheritdoc />
    public async Task<TenantMembershipAddResult> AddAsync(Guid tenantId, Guid userId, IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken = default)
    {
        var grantedRoleIds = roleIds.Distinct().ToList();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Every membership change of a tenant is serialized against the tenant's own row, so two
        // accounts joining an empty tenant at the same moment cannot both be its first member.
        await LockTenantAsync(tenantId, cancellationToken);

        // Asked inside the lock and before the new row exists, so "the tenant has no member" describes
        // the tenant as it stands rather than a state this very membership has already ended.
        var tenantHasNoMember = !await Memberships(tenantId).AsNoTracking().AnyAsync(cancellationToken);

        if (tenantHasNoMember)
        {
            // Provisioning is idempotent: a tenant that already has its administrator role has that
            // role's permissions reconciled rather than a second role created, so asking for it here
            // is safe whether the tenant was created a moment ago or a year ago.
            var administratorRoleId = await tenantAuthorizationService.ProvisionTenantAdministratorRoleAsync(tenantId, cancellationToken);

            if (!grantedRoleIds.Contains(administratorRoleId))
            {
                grantedRoleIds.Add(administratorRoleId);
            }
        }

        var membership = new TenantMembership { UserId = userId };

        // Inside the tenant's own scope the row is attributed to it on save, so the tenant is named
        // once here rather than written onto the row by hand - and the row is saved before the
        // assignments are replaced, because replacing them saves this context as well.
        using (tenantContext.BeginTenant(tenantId))
        {
            dbContext.TenantMemberships.Add(membership);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        // Replacement rather than a plain grant, so that an account rejoining a tenant it was once
        // removed from ends up holding exactly the roles this request names: an assignment left over
        // from the membership it held before is revoked here rather than silently inherited.
        await tenantAuthorizationService.ReplaceTenantRoleAssignmentsAsync(tenantId, userId, grantedRoleIds, cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new TenantMembershipAddResult { Membership = membership, RoleIds = grantedRoleIds };
    }

    /// <inheritdoc />
    public async Task<TenantMembershipChangeOutcome> ReplaceRolesAsync(Guid tenantId, Guid userId, IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken = default)
    {
        var membership = await GetAsync(tenantId, userId, cancellationToken);
        if (membership is null)
        {
            return TenantMembershipChangeOutcome.MembershipNotFound;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Taken before anything is read or written, so the administrator count below is made against a
        // tenant no other membership change can be altering at the same moment. Without it two requests
        // demoting two different administrators would each see the other's administration and leave the
        // tenant with none.
        await LockTenantAsync(tenantId, cancellationToken);

        await tenantAuthorizationService.ReplaceTenantRoleAssignmentsAsync(tenantId, userId, roleIds, cancellationToken);

        // The membership row is written with the assignments rather than left alone: it is what the
        // concurrency token sits on, so a second caller replacing the same member's roles at the same
        // moment loses visibly instead of overwriting a set it never saw. The audit stamp it takes is
        // also how a change is recorded against a membership that carries no state of its own.
        dbContext.TenantMemberships.Update(membership);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Asked after the replacement and inside the same transaction, counting every member with the
        // new assignments in force. Asking before it, or excluding this member from the count, would
        // refuse changes that leave the tenant perfectly well administered - among them the ordinary
        // case of granting administration to the very member being re-roled.
        if (!await tenantAuthorizationService.AnyOtherMemberHoldsTenantAdministrationAsync(tenantId, _everyMember, cancellationToken))
        {
            // Rolled back rather than corrected: the caller refuses the request and persists nothing,
            // so the member's roles stand exactly as they did before it was made.
            await transaction.RollbackAsync(cancellationToken);
            return TenantMembershipChangeOutcome.LastTenantAdministrator;
        }

        await transaction.CommitAsync(cancellationToken);

        return TenantMembershipChangeOutcome.Applied;
    }

    /// <inheritdoc />
    public async Task<TenantMembershipChangeOutcome> RemoveAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var membership = await GetAsync(tenantId, userId, cancellationToken);
        if (membership is null)
        {
            return TenantMembershipChangeOutcome.MembershipNotFound;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Taken before the count below, so two requests removing two different administrators are
        // decided one after the other rather than both against the state before either of them.
        await LockTenantAsync(tenantId, cancellationToken);

        // Asked before anything is written and naming the member on their way out, because
        // administration held by somebody who is leaving must not count towards what survives their
        // departure. Nothing has been written when this refuses, and the transaction is rolled back as
        // it is disposed.
        if (!await tenantAuthorizationService.AnyOtherMemberHoldsTenantAdministrationAsync(tenantId, userId, cancellationToken))
        {
            return TenantMembershipChangeOutcome.LastTenantAdministrator;
        }

        // Soft-deleted rather than erased: the row is retained as the record that this account was
        // once a member, it stops satisfying every membership read from this moment on, and the
        // partial unique index skips it, so the same account may be added to this tenant again.
        dbContext.TenantMemberships.Remove(membership);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Access to the tenant's data goes with the membership, so the roles that conferred it are
        // withdrawn too. Only this tenant's roles are in the set being emptied, which is what leaves
        // the account, its memberships of other tenants and everything it may do inside them intact.
        await tenantAuthorizationService.ReplaceTenantRoleAssignmentsAsync(tenantId, userId, [], cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return TenantMembershipChangeOutcome.Applied;
    }

    /// <summary>
    /// The live memberships of one tenant. Tenant restriction is relaxed by name and the tenant is
    /// stated in the predicate instead, so this reads the same for a caller acting inside the tenant
    /// and for one acting on it from the platform; the soft-delete filter stays in force, so removed
    /// memberships are absent from every question asked through it.
    /// </summary>
    /// <param name="tenantId">The tenant whose memberships are read.</param>
    /// <returns>A composable query over that tenant's live memberships.</returns>
    private IQueryable<TenantMembership> Memberships(Guid tenantId)
        => dbContext.TenantMemberships
            .AcrossAllTenants()
            .Where(membership => membership.TenantId == tenantId);

    /// <summary>
    /// Takes an exclusive lock on the tenant's own row for the rest of the transaction in progress, so
    /// that the membership changes of one tenant happen one at a time.
    /// </summary>
    /// <param name="tenantId">The tenant whose row is locked.</param>
    /// <param name="cancellationToken">Token used to cancel the statement.</param>
    /// <remarks>
    /// The last-administrator guard is a read followed by a write, and under the read-committed
    /// isolation the database runs at, two such sequences can interleave: each demotes or removes a
    /// different administrator, each still sees the other's administration standing while it counts,
    /// and the tenant ends up with none. The membership row's <c>xmin</c> concurrency token cannot
    /// catch that, because the two requests write different rows. Locking the tenant itself is what
    /// serializes them, so the second request counts the administrators only once the first has
    /// committed or rolled back and sees the state its own decision has to be made against.
    /// <para>
    /// The lock is held until the transaction ends and is taken on the tenant rather than on anything
    /// finer, because what is being protected is a count over the whole tenant rather than any one
    /// row of it. It blocks only concurrent membership writes in the same tenant; reads, and every
    /// other tenant, are unaffected. A tenant row that is absent locks nothing and the caller's own
    /// lookup reports it missing, exactly as it would have without this.
    /// </para>
    /// </remarks>
    private Task LockTenantAsync(Guid tenantId, CancellationToken cancellationToken)
        => dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""SELECT 1 FROM tenancy."Tenants" WHERE "Id" = {tenantId} FOR UPDATE""",
            cancellationToken);
}

/// <summary>
/// What became of a membership change that the last-administrator guard, or the membership's own
/// absence, may refuse. Neither refusal is an exception, because both are ordinary answers rather
/// than faults; the caller turns each value into what the requester sees.
/// </summary>
public enum TenantMembershipChangeOutcome
{
    /// <summary>
    /// The change was persisted.
    /// </summary>
    Applied = 0,

    /// <summary>
    /// The account holds no membership of that tenant, so there was nothing to change. The caller
    /// reports this as a missing record: a membership is a record inside a tenant, and one the caller
    /// cannot see is indistinguishable from one that never existed.
    /// </summary>
    MembershipNotFound = 1,

    /// <summary>
    /// The change would have left the tenant with no member holding tenant administration and was
    /// refused; nothing was persisted. The caller reports it with
    /// <see cref="ErrorCodes.LastTenantAdministrator"/>.
    /// </summary>
    LastTenantAdministrator = 2
}

/// <summary>
/// The membership created by <see cref="ITenantMembershipService.AddAsync"/>, together with the roles
/// it was granted. The two are returned as a pair because the granted set is not always the set that
/// was asked for: the first member of a tenant is also given that tenant's administrator role, and
/// the caller reports what was actually granted rather than what it requested.
/// </summary>
public sealed class TenantMembershipAddResult
{
    /// <summary>
    /// Gets the membership row that was created, carrying its assigned identity and audit values.
    /// </summary>
    public TenantMembership Membership { get; init; } = null!;

    /// <summary>
    /// Gets the roles the new member holds in the tenant, without duplicates.
    /// </summary>
    public List<Guid> RoleIds { get; init; } = [];
}