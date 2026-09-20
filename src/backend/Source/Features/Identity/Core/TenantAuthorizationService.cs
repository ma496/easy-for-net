namespace Backend.Features.Identity.Core;

using Backend.Features.Identity.Core.Entities;
using RefreshTokenIssuer = Backend.Features.Identity.Endpoints.Account.TokenService;

/// <summary>
/// The one contract another feature may name to reach what this feature owns: user accounts, the
/// role graph, and the session a signed-in caller carries. Tenant administration needs all three -
/// it puts an existing account into a tenant, grants and replaces that member's roles inside that
/// tenant alone, and re-establishes the caller's session when the active tenant changes - while the
/// account, role and token types themselves stay inside this feature: every member below speaks in
/// <see cref="Guid"/>, <see cref="string"/> and the tenancy DTOs that belong to neither feature.
/// </summary>
/// <remarks>
/// Every member names the tenant it acts for. The caller is often a platform administrator acting on
/// a tenant other than the one their own session is in, so nothing here reads the active tenant
/// scope to decide which tenant is meant - and nothing here decides whether the caller is entitled
/// to act on that tenant, which is the calling endpoint's own guard.
/// </remarks>
[AllowOutside]
public interface ITenantAuthorizationService
{
    /// <summary>
    /// Tells whether a user account exists at all, so that a membership requested for an account
    /// that does not exist is refused rather than created against nothing.
    /// </summary>
    /// <param name="userId">The account being looked for.</param>
    /// <param name="cancellationToken">Token used to cancel the read.</param>
    /// <returns><see langword="true"/> when the account exists.</returns>
    Task<bool> UserExistsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Provisions a tenant with its system-created administrator role - the role holding every
    /// permission the code-declared catalogue makes exercisable inside a tenant, and no platform-scoped
    /// one - and returns its identifier so the caller can grant it to the tenant's first member. Running this
    /// again for a tenant that already has the role reconciles that role's permissions instead of
    /// creating a second one, so provisioning is safe to repeat.
    /// </summary>
    /// <param name="tenantId">The tenant being provisioned.</param>
    /// <param name="cancellationToken">Token used to cancel the reads and the writes.</param>
    /// <returns>The identifier of the tenant's administrator role.</returns>
    Task<Guid> ProvisionTenantAdministratorRoleAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces a member's role assignments inside one tenant with exactly the roles supplied,
    /// granting the ones they do not hold and revoking the ones that have left the set. Assignments
    /// to another tenant's roles, and to roles that belong to no tenant, lie outside the set being
    /// replaced, so a change made in one tenant never reaches the member's standing in another.
    /// </summary>
    /// <param name="tenantId">The tenant whose assignments are being replaced.</param>
    /// <param name="userId">The member whose assignments are being replaced.</param>
    /// <param name="roleIds">The roles the member is to hold in that tenant, and no others.</param>
    /// <param name="cancellationToken">Token used to cancel the reads and the writes.</param>
    /// <remarks>
    /// This writes through the caller's own unit of work and opens no transaction of its own, so the
    /// caller carries the replacement and whatever else the operation changes - the membership row
    /// whose concurrency token records the change among them - in one transaction. It does save, so
    /// anything the caller has staged on the same context and not yet saved is flushed along with it:
    /// stage a tenant-scoped row only inside the scope it belongs to, or save it before calling.
    /// </remarks>
    Task ReplaceTenantRoleAssignmentsAsync(Guid tenantId, Guid userId, IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tells whether any member of a tenant, other than the one named, holds tenant administration
    /// there. This is the reading behind the last-administrator refusal: a tenant none of whose
    /// counted members holds the permission is administrable by nobody from inside it.
    /// </summary>
    /// <param name="tenantId">The tenant being examined.</param>
    /// <param name="excludedUserId">
    /// The member left out of the count, or <see cref="Guid.Empty"/> to count every member.
    /// </param>
    /// <param name="cancellationToken">Token used to cancel the read.</param>
    /// <returns><see langword="true"/> when a counted member holds tenant administration.</returns>
    /// <remarks>
    /// The two callers ask this differently, and the difference is the whole of the guard.
    /// <para>
    /// Removing a membership asks before the change, naming the member being removed: that member is
    /// on their way out, so administration they hold must not count towards what survives.
    /// </para>
    /// <para>
    /// Replacing a member's role assignments must not ask that way. The set being granted may itself
    /// keep or confer administration, so excluding that member would refuse a change that leaves the
    /// tenant perfectly well administered. That caller applies the replacement inside its own
    /// transaction and then asks with <see cref="Guid.Empty"/> - counting every member, this one
    /// included, with the new assignments in force - rolling the transaction back and refusing only
    /// when the answer is <see langword="false"/>.
    /// </para>
    /// </remarks>
    Task<bool> AnyOtherMemberHoldsTenantAdministrationAsync(Guid tenantId, Guid excludedUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tells whether an account may exercise one named permission inside one named tenant: it holds a
    /// live membership there, and a role it holds there - or a platform-scoped role, which belongs to
    /// no tenant and so applies in all of them - grants that permission. Every permission asked about
    /// here is one exercisable inside a tenant, so the scope narrowing the session applies would change
    /// nothing and is not repeated.
    /// </summary>
    /// <param name="userId">The account whose standing is examined.</param>
    /// <param name="tenantId">The tenant the permission must be held in.</param>
    /// <param name="permission">The permission being asked about, from <see cref="Allow"/>.</param>
    /// <param name="cancellationToken">Token used to cancel the read.</param>
    /// <returns><see langword="true"/> when the account may exercise that permission in that tenant.</returns>
    /// <remarks>
    /// This exists because a request's permission claims describe the tenant its session is acting in,
    /// while a surface that takes the tenant from its route acts on a tenant that may be another one
    /// entirely. Membership of the route's tenant is not enough to authorize such a request: an account
    /// can be an administrator of one tenant and an ordinary member of the next, so the permission has
    /// to be read for the tenant actually being administered rather than for the one the claims were
    /// minted in. A platform account acting in platform scope is authorized by its tier and never
    /// reaches this question.
    /// </remarks>
    Task<bool> HoldsTenantPermissionAsync(Guid userId, Guid tenantId, string permission, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tells whether an account's standing reaches beyond the tenant being acted in: it holds a
    /// membership of some other tenant, or a platform-scoped role. Such an account is a shared
    /// identity, so changes that would follow it everywhere - its name, whether it is active at all,
    /// its deletion - are not one tenant's to make.
    /// </summary>
    /// <param name="userId">The account being examined.</param>
    /// <param name="tenantId">The tenant being acted in, which is left out of the comparison.</param>
    /// <param name="cancellationToken">Token used to cancel the read.</param>
    /// <returns><see langword="true"/> when the account belongs to more than this one tenant.</returns>
    Task<bool> ReachesBeyondTenantAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns one page of the accounts holding an active membership of a tenant, each carrying the
    /// roles it holds in that tenant alone, sorted, searched and paged as the request asks.
    /// </summary>
    /// <param name="tenantId">The tenant whose members are listed.</param>
    /// <param name="request">Paging, sorting and search criteria; their bounds are enforced by the calling endpoint's validator.</param>
    /// <param name="roleId">When supplied, limits the page to members holding that role in this tenant.</param>
    /// <param name="cancellationToken">Token used to cancel the reads.</param>
    /// <returns>The requested page and the total number of members matching the request.</returns>
    Task<TenantMemberPageDto> GetTenantMembersAsync(Guid tenantId, ListRequestDto<Guid> request, Guid? roleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts how many accounts hold an active membership of each of the tenants named, so a list of
    /// tenants can report the size of each without reading a page of members for every row.
    /// </summary>
    /// <param name="tenantIds">The tenants being counted - in practice the page being listed, not every tenant there is.</param>
    /// <param name="cancellationToken">Token used to cancel the read.</param>
    /// <returns>A count per tenant. A tenant with no members is absent from the result rather than present with zero.</returns>
    /// <remarks>
    /// The members counted are the same ones <see cref="GetTenantMembersAsync"/> lists: accounts that
    /// still exist and hold a membership that has not been removed. Membership rows are deliberately
    /// not counted on their own, because deleting an account leaves its membership row behind and
    /// counting rows would report a member the members screen does not show.
    /// </remarks>
    Task<Dictionary<Guid, int>> GetTenantMemberCountsAsync(IReadOnlyCollection<Guid> tenantIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tells whether every role named belongs to one tenant, so that a request assigning a role of
    /// another tenant - or a role that belongs to none - is refused before anything is written. An
    /// empty set belongs to every tenant trivially.
    /// </summary>
    /// <param name="tenantId">The tenant the roles must belong to.</param>
    /// <param name="roleIds">The roles being checked.</param>
    /// <param name="cancellationToken">Token used to cancel the read.</param>
    /// <returns><see langword="true"/> when every role named belongs to that tenant.</returns>
    Task<bool> AllRolesBelongToTenantAsync(Guid tenantId, IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-establishes an authenticated account's session so that it carries the tenant named, leaving
    /// the account signed in and asking for no credentials. The roles and permissions it embeds are
    /// recomputed from the membership and role assignments as they stand at this moment for that
    /// tenant alone, the cookie principal is re-signed with them, a fresh access/refresh pair is
    /// issued, and the tenant is recorded on the refresh-token row so that a later refresh
    /// re-establishes this tenant rather than the one the account was acting in before. The pair the
    /// request arrived with is revoked as the new one is issued, so the refresh token the caller held a
    /// moment ago cannot afterwards be redeemed for a session back in the previous tenant; the
    /// account's sessions on other devices are untouched.
    /// </summary>
    /// <param name="userId">The account whose session is re-established.</param>
    /// <param name="tenantId">The tenant the session is to act in, or <see langword="null"/> for none.</param>
    /// <param name="cancellationToken">Token used to cancel the reads.</param>
    /// <returns>The new session material, which a JWT client replaces its token pair with and a cookie client ignores.</returns>
    Task<TenantSessionDto> ReissueSessionAsync(Guid userId, Guid? tenantId, CancellationToken cancellationToken = default);
}

/// <summary>
/// EF Core-backed implementation of <see cref="ITenantAuthorizationService"/>. Every read that spans
/// tenants relaxes tenant restriction by name and states the tenant it means in its own predicate,
/// and every write that lands on a tenant-scoped row happens inside that tenant's scope, so the
/// service behaves the same for a caller acting inside the tenant and for a platform administrator
/// acting on it from outside.
/// </summary>
[NoDirectUse]
public class TenantAuthorizationService(AppDbContext dbContext,
                                        IUserService userService,
                                        IRoleService roleService,
                                        IPermissionService permissionService,
                                        IPermissionDefinitionService permissionDefinitionService,
                                        ITenantContext tenantContext,
                                        IHttpContextAccessor httpContextAccessor,
                                        IAuthTokenService authTokenService,
                                        RefreshTokenIssuer refreshTokenIssuer) : ITenantAuthorizationService
{
    /// <summary>
    /// Name every tenant's system-created administrator role carries. A role name is unique within
    /// its tenant, so each tenant's role holds this name without colliding with any other's, and the
    /// bootstrap tenant's role - provisioned by the seeder before this service ever runs - is found
    /// by that same name.
    /// </summary>
    private const string AdministratorRoleName = "Admin";
    private const string AdministratorRoleNameNormalized = "admin";
    private const string AdministratorRoleDescription = "Tenant Admin Role";

    /// <summary>
    /// The permission that constitutes tenant administration. Replacing a member's role assignments
    /// is the one operation that can put administration back into anybody's hands, so a tenant that
    /// lost its last holder of it would be administrable by nobody from inside - exactly the state
    /// the last-administrator guard exists to prevent.
    /// </summary>
    private const string TenantAdministrationPermission = Allow.TenantMember_UpdateRoles;

    /// <inheritdoc />
    public Task<bool> UserExistsAsync(Guid userId, CancellationToken cancellationToken = default)
        // Read off the set rather than through the user service: an account belongs to no tenant of
        // its own, but the user service narrows its lookups to the tenant the caller is acting in, and
        // the account asked about here is by definition not a member of the tenant yet - so a narrowed
        // lookup would report an account that exists as missing.
        => dbContext.Users
            .AsNoTracking()
            .AnyAsync(account => account.Id == userId, cancellationToken);

    /// <inheritdoc />
    public async Task<Guid> ProvisionTenantAdministratorRoleAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        // A tenant role may hold only what can be exercised inside a tenant, so the catalogue is
        // narrowed here rather than filtered out wherever the role is later read.
        var tenantPermissionNames = permissionDefinitionService.GetPermissionNamesInScope(PermissionScope.Tenant).ToList();

        // Inside the tenant's own scope the role lookup is restricted to that tenant and the save
        // attributes the new row to it, so neither has to name the tenant a second time - and a role
        // of the same name in another tenant is neither found nor disturbed.
        using (tenantContext.BeginTenant(tenantId))
        {
            var administratorRole = await dbContext.Roles
                    .FirstOrDefaultAsync(role => role.NameNormalized == AdministratorRoleNameNormalized, cancellationToken)
                ?? await roleService.CreateAsync(new Role
                {
                    SystemCreated = true,
                    Name = AdministratorRoleName,
                    Description = AdministratorRoleDescription
                });

            var tenantPermissionIds = await dbContext.Permissions
                .AsNoTracking()
                .Where(permission => tenantPermissionNames.Contains(permission.Name))
                .Select(permission => permission.Id)
                .ToListAsync(cancellationToken);

            var heldPermissionIds = (await permissionService.GetRolePermissionsAsync(administratorRole.Id))
                .Select(permission => permission.Id)
                .ToList();

            var missingPermissionIds = tenantPermissionIds.Except(heldPermissionIds).ToList();
            if (missingPermissionIds.Count > 0)
            {
                await roleService.AssignPermissionsAsync(administratorRole.Id, missingPermissionIds);
            }

            // Reconciliation runs both ways, exactly as the seeder reconciles the bootstrap tenant's
            // role: a permission the catalogue has since moved to the platform scope, or stopped
            // declaring at all, comes off the role rather than being left behind, so a tenant role can
            // never end up holding a platform-scoped permission.
            var withdrawnPermissionIds = heldPermissionIds.Except(tenantPermissionIds).ToList();
            if (withdrawnPermissionIds.Count > 0)
            {
                await roleService.RemovePermissionsAsync(administratorRole.Id, withdrawnPermissionIds);
            }

            return administratorRole.Id;
        }
    }

    /// <inheritdoc />
    public async Task ReplaceTenantRoleAssignmentsAsync(Guid tenantId, Guid userId, IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken = default)
    {
        var tenantRoleIds = await dbContext.Roles
            .AsNoTracking()
            .AcrossAllTenants()
            .Where(role => role.TenantId == tenantId)
            .Select(role => role.Id)
            .ToListAsync(cancellationToken);

        // Only assignments to this tenant's roles are read, so the member's roles in every other
        // tenant - and any platform-scoped role they hold - are outside the set being replaced and
        // survive it untouched.
        var heldAssignments = await dbContext.UserRoles
            .Where(assignment => assignment.UserId == userId && tenantRoleIds.Contains(assignment.RoleId))
            .ToListAsync(cancellationToken);

        // A role that does not belong to the tenant is dropped rather than granted. The caller refuses
        // such a request outright with a referenced-record error, and honouring one here would be a
        // grant reaching across the tenant boundary.
        var requestedRoleIds = roleIds.Distinct().Where(roleId => tenantRoleIds.Contains(roleId)).ToHashSet();

        var revoked = heldAssignments.Where(assignment => !requestedRoleIds.Contains(assignment.RoleId)).ToList();
        if (revoked.Count > 0)
        {
            dbContext.UserRoles.RemoveRange(revoked);
        }

        var granted = requestedRoleIds
            .Where(roleId => heldAssignments.All(assignment => assignment.RoleId != roleId))
            .Select(roleId => new UserRole { UserId = userId, RoleId = roleId })
            .ToList();
        if (granted.Count > 0)
        {
            dbContext.UserRoles.AddRange(granted);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> AnyOtherMemberHoldsTenantAdministrationAsync(Guid tenantId, Guid excludedUserId, CancellationToken cancellationToken = default)
    {
        // The tenant's own roles that grant administration. The soft-delete filter stays in force, so
        // a deleted role keeps nobody in administration, and the tenant predicate is what stops a role
        // of another tenant from counting towards this one.
        var administratorRoleIds = dbContext.Roles
            .AsNoTracking()
            .AcrossAllTenants()
            .Where(role => role.TenantId == tenantId
                           && role.RolePermissions.Any(rolePermission => rolePermission.Permission.Name == TenantAdministrationPermission))
            .Select(role => role.Id);

        // Removed memberships are soft deleted, so only members who still hold one are counted, and a
        // deactivated account is not counted at all: it cannot sign in, so administration it holds is
        // administration nobody can exercise, and a tenant left with only such members is exactly the
        // unadministrable state this guard exists to prevent.
        return await dbContext.TenantMemberships
            .AsNoTracking()
            .AcrossAllTenants()
            .AnyAsync(membership => membership.TenantId == tenantId
                                    && membership.UserId != excludedUserId
                                    && dbContext.Users.Any(account => account.Id == membership.UserId && account.IsActive)
                                    && dbContext.UserRoles.Any(assignment => assignment.UserId == membership.UserId
                                                                             && administratorRoleIds.Contains(assignment.RoleId)),
                cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> HoldsTenantPermissionAsync(Guid userId, Guid tenantId, string permission, CancellationToken cancellationToken = default)
    {
        // The membership and the grant are asked as one query rather than two, because both are read on
        // every request that administers a tenant named by its route and neither answer is useful
        // without the other.
        //
        // The roles that count are this tenant's own and the platform-scoped ones, which is exactly the
        // set a session is minted with for that tenant, so a caller is authorized here
        // for a tenant precisely when a session acting in that tenant would be. The soft-delete filter
        // stays in force on all three sets, so a deleted role grants nothing and a removed membership
        // places nobody.
        return dbContext.Roles
            .AsNoTracking()
            .AcrossAllTenants()
            .AnyAsync(role => (role.TenantId == tenantId || role.TenantId == null)
                              && role.RolePermissions.Any(rolePermission => rolePermission.Permission.Name == permission)
                              && dbContext.UserRoles.Any(assignment => assignment.UserId == userId && assignment.RoleId == role.Id)
                              && dbContext.TenantMemberships.AcrossAllTenants()
                                  .Any(membership => membership.TenantId == tenantId && membership.UserId == userId),
                cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> ReachesBeyondTenantAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        // A membership of another tenant makes the account that tenant's member too. The read relaxes
        // tenant restriction by name, because the rows being looked for deliberately belong to tenants
        // other than the one being acted in.
        var belongsElsewhere = await dbContext.TenantMemberships
            .AsNoTracking()
            .AcrossAllTenants()
            .AnyAsync(membership => membership.UserId == userId && membership.TenantId != tenantId, cancellationToken);

        if (belongsElsewhere)
        {
            return true;
        }

        // A platform-scoped role belongs to no tenant and confers its permissions in every one, so an
        // account holding it is the platform's rather than any single tenant's.
        return await dbContext.Roles
            .AsNoTracking()
            .AcrossAllTenants()
            .AnyAsync(role => role.TenantId == null
                              && dbContext.UserRoles.Any(assignment => assignment.UserId == userId && assignment.RoleId == role.Id),
                cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Dictionary<Guid, int>> GetTenantMemberCountsAsync(IReadOnlyCollection<Guid> tenantIds, CancellationToken cancellationToken = default)
    {
        // Nothing to ask the database when the page is empty, and an empty IN list is not a query worth
        // sending.
        if (tenantIds.Count == 0)
        {
            return [];
        }

        // One grouped read for the whole page rather than one count per row. Tenant restriction is
        // relaxed by name because the memberships being counted belong to the tenants named here and
        // not to whichever tenant the caller is acting in; the soft-delete filter stays in force on
        // both sets, so a removed membership counts nobody and a deleted account is not counted at
        // all - which is what keeps this number equal to the total GetTenantMembersAsync reports.
        var counts = await dbContext.TenantMemberships
            .AsNoTracking()
            .AcrossAllTenants()
            .Where(membership => membership.TenantId != null
                                 && tenantIds.Contains(membership.TenantId.Value)
                                 && dbContext.Users.Any(account => account.Id == membership.UserId))
            .GroupBy(membership => membership.TenantId)
            .Select(group => new { TenantId = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        return counts.ToDictionary(row => row.TenantId!.Value, row => row.Count);
    }

    /// <inheritdoc />
    public async Task<TenantMemberPageDto> GetTenantMembersAsync(Guid tenantId, ListRequestDto<Guid> request, Guid? roleId, CancellationToken cancellationToken = default)
    {
        // Both sets are named once and reused: as the predicate deciding which accounts are members,
        // and as the source of the per-tenant audit values and roles each row carries.
        var memberships = dbContext.TenantMemberships
            .AsNoTracking()
            .AcrossAllTenants()
            .Where(membership => membership.TenantId == tenantId);

        var tenantRoles = dbContext.Roles
            .AsNoTracking()
            .AcrossAllTenants()
            .Where(role => role.TenantId == tenantId);

        var query = dbContext.Users
            .AsNoTracking()
            .Where(account => memberships.Any(membership => membership.UserId == account.Id));

        var search = request.Search?.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(account => EF.Functions.Like(account.UsernameNormalized, $"%{search}%")
                                           || EF.Functions.Like(account.EmailNormalized, $"%{search}%"));
        }

        if (roleId.HasValue)
        {
            query = query.Where(account => tenantRoles.Any(role => role.Id == roleId.Value
                                                                   && role.UserRoles.Any(assignment => assignment.UserId == account.Id)));
        }

        // Counted before paging, so the caller can tell how many pages remain.
        var total = await query.CountAsync(cancellationToken);

        var rows = await query
            .Process(request)
            .Select(account => new
            {
                account.Id,
                account.Username,
                account.Email,
                account.FirstName,
                account.LastName,
                account.IsActive,
                Membership = memberships
                    .Where(membership => membership.UserId == account.Id)
                    .Select(membership => new
                    {
                        membership.CreatedAt,
                        membership.CreatedBy,
                        membership.UpdatedAt,
                        membership.UpdatedBy
                    })
                    .FirstOrDefault(),
                Roles = tenantRoles
                    .Where(role => role.UserRoles.Any(assignment => assignment.UserId == account.Id))
                    .Select(role => new TenantMemberRoleInfo { Id = role.Id, Name = role.Name })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        // The audit values a row reports are the membership's rather than the account's, so the same
        // account described for two tenants carries each tenant's own history. The membership cannot
        // be missing - it is what put the account in the page - but the projection is optional to the
        // compiler, so the fallbacks below stand in for a row that cannot occur.
        return new TenantMemberPageDto
        {
            Items =
            [
                .. rows.Select(row => new TenantMemberDto
                {
                    UserId = row.Id,
                    Username = row.Username,
                    Email = row.Email,
                    FirstName = row.FirstName,
                    LastName = row.LastName,
                    IsActive = row.IsActive,
                    MemberSince = row.Membership?.CreatedAt ?? default(DateTime),
                    CreatedAt = row.Membership?.CreatedAt ?? default(DateTime),
                    CreatedBy = row.Membership?.CreatedBy,
                    UpdatedAt = row.Membership?.UpdatedAt,
                    UpdatedBy = row.Membership?.UpdatedBy,
                    Roles = row.Roles
                })
            ],
            Total = total
        };
    }

    /// <inheritdoc />
    public async Task<bool> AllRolesBelongToTenantAsync(Guid tenantId, IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken = default)
    {
        var requestedRoleIds = roleIds.Distinct().ToList();
        if (requestedRoleIds.Count == 0)
        {
            return true;
        }

        // Counting the matches rather than hunting for a stray one is what makes an id naming no role
        // at all fail the check exactly as an id naming another tenant's role does.
        var matched = await dbContext.Roles
            .AsNoTracking()
            .AcrossAllTenants()
            .CountAsync(role => role.TenantId == tenantId && requestedRoleIds.Contains(role.Id), cancellationToken);

        return matched == requestedRoleIds.Count;
    }

    /// <inheritdoc />
    public async Task<TenantSessionDto> ReissueSessionAsync(Guid userId, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        var account = await userService.GetByIdAsync(userId)
            ?? throw new InvalidOperationException($"A session cannot be re-established for the unknown account '{userId}'.");

        // What the re-established session may do is read from current data for the tenant being
        // established and never copied from the session it replaces: the role assignments and the
        // roles' permissions as they stand at this moment decide it, so authority the caller held in
        // the tenant they acted in a moment ago is not carried into this one.
        var grants = await SessionGrants.ReadAsync(dbContext, userId, tenantId, account.IsPlatform, cancellationToken);

        var claims = Helper.CreateClaims(account, grants.Roles, grants.Permissions, tenantId);

        // for cookie authentication
        await CookieAuth.SignInAsync(user => user.Claims.AddRange(claims));

        if (httpContextAccessor.HttpContext is { } httpContext)
        {
            // The session being replaced is ended before its successor is issued. Its refresh-token row
            // carries the tenant the account was acting in a moment ago, so leaving the row in place
            // would leave that tenant redeemable: whoever held the old refresh token could refresh back
            // into it until it expired, and every switch would leave another such row behind.
            await RevokeSupersededSessionAsync(userId, httpContext, cancellationToken);

            // Recorded before the pair is issued, because the row written for the new refresh token is
            // the only record a later refresh has of the tenant this session acts in - and it is what
            // stops that refresh from resurrecting the tenant the account acted in before.
            RefreshTokenIssuer.RecordSessionTenant(httpContext, tenantId);
        }

        // for jwt authentication. The issuer persists the pair, writes the recorded tenant onto its
        // row and rewrites the refreshToken cookie, so a re-established session is stored and carried
        // exactly the way a freshly signed-in one is.
        return await refreshTokenIssuer.CreateCustomToken(
            userId.ToString(),
            user => user.Claims.AddRange(claims),
            response => new TenantSessionDto
            {
                UserId = userId,
                AccessToken = response.AccessToken,
                AccessTokenExpiry = response.AccessExpiry,
                RefreshToken = response.RefreshToken,
                RefreshTokenExpiry = response.RefreshExpiry
            });
    }

    /// <summary>
    /// Revokes the stored token pair the request arrived with, so that the session being replaced
    /// cannot be refreshed after its successor has been issued.
    /// </summary>
    /// <param name="userId">The account whose session is being replaced.</param>
    /// <param name="httpContext">The request the replacement is being issued on.</param>
    /// <param name="cancellationToken">Token used to cancel the delete.</param>
    /// <remarks>
    /// The refresh token is read from the cookie the browser carries it in, which holds it as
    /// <c>UserId:RefreshToken</c>. A request that carries no such cookie - a client holding its token
    /// pair itself rather than in cookies - leaves nothing to revoke here, and its old pair stays
    /// valid until it expires or is next refreshed. The delete names the account as well as the token,
    /// so a cookie belonging to somebody else revokes nothing.
    /// </remarks>
    private async Task RevokeSupersededSessionAsync(Guid userId, HttpContext httpContext, CancellationToken cancellationToken)
    {
        if (!httpContext.Request.Cookies.TryGetValue(RefreshTokenIssuer.RefreshTokenCookieName, out var cookieValue)
            || string.IsNullOrWhiteSpace(cookieValue))
        {
            return;
        }

        var separatorIndex = cookieValue.IndexOf(':');
        var refreshToken = separatorIndex >= 0 ? cookieValue[(separatorIndex + 1)..] : cookieValue;

        await authTokenService.RevokeRefreshTokenAsync(userId, refreshToken, cancellationToken);
    }

}