namespace Backend.Features.Tenancy.Core;

using Backend.ShareData.Entities;
using Backend.Features.Identity.Core;
using Backend.Features.Tenancy.Core.Entities;

/// <summary>
/// Reads and creates tenant records. A tenant is the scope rather than something inside one, so
/// nothing here is restricted by the active tenant: which tenants a caller may see is decided by the
/// caller's own standing - the platform tier in platform scope, or an active membership - and that decision lives
/// in <see cref="Tenants"/> alone, so no lookup, list or search can disagree with another about it.
/// </summary>
/// <remarks>
/// <see cref="CreateAsync"/> is the single creation path. A tenant created by a platform
/// administrator and a tenant created alongside the account that signs itself up differ only in whether a first
/// member is named: the trimming, the state the row is persisted in, the administrator role the
/// tenant is provisioned with and the identifier comparison that guards it are the same code for
/// both, so the two surfaces cannot drift apart.
/// </remarks>
[AllowOutside]
public interface ITenantService
{
    /// <summary>
    /// The message reported when an identifier is already taken. Both creation surfaces and the
    /// update surface raise it against their own identifier field with
    /// <see cref="ErrorCodes.TenantIdentifierAlreadyExists"/>, so a duplicate reads the same however
    /// it was submitted.
    /// </summary>
    const string DuplicateIdentifierMessage = "Tenant identifier already exists";

    /// <summary>
    /// The tenants the caller may see: every tenant that is not deleted for a caller holding platform
    /// administration, and otherwise only the tenants the caller holds an active membership in.
    /// Lookups, lists, searches and counts all narrow from this one query, so none of them can forget
    /// the restriction.
    /// </summary>
    /// <returns>A composable query over the tenants the caller may see.</returns>
    /// <remarks>
    /// Deleted tenants are excluded here as everywhere else, and a membership that has been removed is
    /// soft-deleted and so keeps nobody inside a tenant. A tenant the caller may not see is therefore
    /// indistinguishable from one that never existed, which is what lets every surface refuse it with
    /// the same not-found error rather than disclosing that it is somebody else's.
    /// </remarks>
    IQueryable<Tenant> Tenants();

    /// <summary>
    /// The tenant with this id that the caller may see, or <see langword="null"/> when there
    /// is none - which a tenant the caller does not belong to is, exactly as a deleted or absent one
    /// is.
    /// </summary>
    /// <param name="id">The id of the tenant being read - its primary key, never its url-safe identifier.</param>
    /// <param name="cancellationToken">Token used to cancel the read.</param>
    /// <returns>The tenant, or <see langword="null"/> when the caller may not see it.</returns>
    Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tells whether an identifier is already taken. The comparison runs against the stored normalized
    /// lower-case form, so two identifiers differing only in case or in surrounding white-space are the
    /// same identifier, and it deliberately includes soft-deleted tenants, so an identifier freed only
    /// by deletion cannot be taken again. The unique index on that same column is the backstop when
    /// two requests ask at once.
    /// </summary>
    /// <param name="identifier">The identifier being checked, as the caller entered it.</param>
    /// <param name="excludedTenantId">
    /// The tenant left out of the comparison - the one being renamed, so that keeping its own
    /// identifier is not reported as a duplicate - or <see langword="null"/> to compare against every
    /// tenant.
    /// </param>
    /// <param name="cancellationToken">Token used to cancel the read.</param>
    /// <returns><see langword="true"/> when a tenant already holds the identifier.</returns>
    /// <remarks>
    /// Callers refuse the request against their own identifier field with
    /// <see cref="DuplicateIdentifierMessage"/> and
    /// <see cref="ErrorCodes.TenantIdentifierAlreadyExists"/>, and persist nothing.
    /// </remarks>
    Task<bool> IdentifierExistsAsync(string identifier, Guid? excludedTenantId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a tenant in the active state, with its display name and identifier trimmed as entered
    /// and the normalized identifier maintained beside it, and provisions it with its system-created
    /// administrator role - the role holding every tenant-tier permission - so that a tenant is never
    /// left without one. When a first member is named, that account is given an active membership of
    /// the new tenant and assigned that role, which is what makes a self-service creator the
    /// administrator of the tenant created with their account.
    /// </summary>
    /// <param name="tenant">The tenant to create, carrying the display name and identifier alone.</param>
    /// <param name="firstMemberUserId">
    /// The account to make the tenant's first member and administrator, or <see langword="null"/> for
    /// a tenant created from the platform, which has no member until one is added.
    /// </param>
    /// <param name="cancellationToken">Token used to cancel the reads and the writes.</param>
    /// <returns>The created tenant, carrying its assigned identity and audit values.</returns>
    /// <remarks>
    /// The caller checks the identifier with <see cref="IdentifierExistsAsync"/> first and refuses a
    /// duplicate itself, because that refusal is a field-level validation failure on the request the
    /// caller owns. Everything this writes happens in one transaction, so a tenant is never left
    /// half-provisioned: either the row, its administrator role and the first member's assignment are
    /// all persisted, or none of them is.
    /// </remarks>
    Task<Tenant> CreateAsync(Tenant tenant, Guid? firstMemberUserId = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// EF Core-backed implementation of <see cref="ITenantService"/>. The role provisioning and the role
/// assignment a new tenant needs belong to the identity slice and are reached through the one
/// contract that slice publishes, so no account, role or token type is named here.
/// </summary>
[NoDirectUse]
public class TenantService(AppDbContext dbContext,
                           ICurrentUserService currentUserService,
                           ITenantAuthorizationService tenantAuthorizationService,
                           ITenantContext tenantContext) : ITenantService
{
    /// <summary>
    /// Key of the soft-delete query filter, as <see cref="AppDbContext"/> registers it. The duplicate
    /// comparison suppresses that one filter by name so that it sees retained rows, which is what
    /// keeps a deleted tenant's identifier reserved.
    /// </summary>
    private const string SoftDeleteFilterKey = "SoftDelete";

    /// <inheritdoc />
    public IQueryable<Tenant> Tenants()
    {
        // A platform account acting in no tenant sees every tenant there is - that is what the tenants
        // table is. Inside a tenant it sees what its membership would show it, because entering a tenant
        // makes it that tenant's actor; the tier alone is not the test, the scope is part of it.
        if (currentUserService.IsPlatform() && tenantContext.IsPlatformScope())
        {
            return dbContext.Tenants;
        }

        // An unauthenticated caller sees no tenants at all. Standing in for a missing identity with an
        // identifier no account can have keeps that a plain predicate rather than a special case, and
        // keeps the whole condition translatable to SQL.
        var callerId = currentUserService.GetCurrentUserId() ?? Guid.Empty;

        // Memberships are tenant-scoped, and the caller may be acting in another tenant - or in none at
        // all - while asking which tenants they belong to, so tenant restriction is relaxed by name and
        // the membership's own tenant is stated in the predicate instead. The soft-delete filter stays
        // in force on both sides: a removed membership keeps nobody inside a tenant, and a deleted
        // tenant is listed by nobody.
        var callerMemberships = dbContext.TenantMemberships
            .AcrossAllTenants()
            .Where(membership => membership.UserId == callerId);

        return dbContext.Tenants
            .Where(tenant => callerMemberships.Any(membership => membership.TenantId == tenant.Id));
    }

    /// <inheritdoc />
    public Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        // Narrowed from Tenants() rather than read off the set, so a tenant the caller has no standing
        // in reads as missing here exactly as it does in the list.
        => Tenants().FirstOrDefaultAsync(tenant => tenant.Id == id, cancellationToken);

    /// <inheritdoc />
    public Task<bool> IdentifierExistsAsync(string identifier, Guid? excludedTenantId = null, CancellationToken cancellationToken = default)
    {
        // Normalized the way the entity normalizes the column being compared, so the comparison asks
        // the question the stored form answers.
        var normalizedIdentifier = identifier.Trim().ToLowerInvariant();

        var query = dbContext.Tenants
            .AsNoTracking()
            .IgnoreQueryFilters([SoftDeleteFilterKey])
            .Where(tenant => tenant.IdentifierNormalized == normalizedIdentifier);

        if (excludedTenantId is { } excludedId)
        {
            query = query.Where(tenant => tenant.Id != excludedId);
        }

        return query.AnyAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Tenant> CreateAsync(Tenant tenant, Guid? firstMemberUserId = null, CancellationToken cancellationToken = default)
    {
        // Stored as entered but trimmed, so the display name and identifier a caller reads back are the
        // ones the length and shape rules were applied to. The normalized identifier beside them is
        // recomputed by the entity itself on save.
        tenant.Name = tenant.Name.Trim();
        tenant.Identifier = tenant.Identifier.Trim();

        // Neither of these is taken from the request: a tenant is born active, and only the seeder's
        // bootstrap tenant is system-created, so no payload can claim either.
        tenant.Status = TenantStatus.Active;
        tenant.SystemCreated = false;

        // A caller that has already opened a transaction - self-service sign-up, which creates the
        // account and its tenant as one act - carries this work inside that transaction, so the two
        // commit or roll back together. Opening a second transaction while one is in force is refused
        // by the provider outright, so the ambient one is joined rather than nested, and only the
        // transaction this call opened is the one this call commits.
        await using var transaction = dbContext.Database.CurrentTransaction is null
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        // A tenant belongs to no tenant, so this row needs no scope established for it; the creating
        // account and the creation time are stamped centrally on save.
        dbContext.Tenants.Add(tenant);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Provisioned before any member exists, so the role is there to grant to the first one - and so
        // a tenant created from the platform, which has no member yet, is administrable the moment one
        // is added.
        var administratorRoleId = await tenantAuthorizationService.ProvisionTenantAdministratorRoleAsync(tenant.Id, cancellationToken);

        if (firstMemberUserId is { } firstMemberId)
        {
            // Inside the new tenant's scope the membership row is attributed to it on save, so the
            // tenant is named once here rather than written onto the row by hand. The row is saved
            // before the assignment because the assignment saves this context as well.
            using (tenantContext.BeginTenant(tenant.Id))
            {
                dbContext.TenantMemberships.Add(new TenantMembership { UserId = firstMemberId });
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            // The first member holds the administrator role in this tenant and nothing else here, which
            // confers authority inside the new tenant alone: that role carries no platform-tier
            // permission, and the member's standing in every other tenant lies outside the set being
            // replaced.
            await tenantAuthorizationService.ReplaceTenantRoleAssignmentsAsync(tenant.Id, firstMemberId, [administratorRoleId], cancellationToken);
        }

        // Nothing to commit when the transaction belongs to the caller: it commits when the whole act
        // the tenant is part of has succeeded, and rolls this back with it when it has not.
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return tenant;
    }
}
