namespace Backend.Features.Identity.Core;

using System.Security.Claims;
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
    /// tenant-tier permission the code-declared catalogue contains and no platform-tier one - and
    /// returns its identifier so the caller can grant it to the tenant's first member. Running this
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
    /// re-establishes this tenant rather than the one the account was acting in before.
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

    /// <summary>
    /// Authentication type of the principal a re-established session is evaluated as. It never
    /// authenticates a request; it only names the identity built to ask what that session is
    /// entitled to in the tenant being established.
    /// </summary>
    private const string ReissueAuthenticationType = "TenantSessionReissue";

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
        // A tenant role may hold no platform-tier permission, so the platform tier is subtracted from
        // the catalogue here rather than filtered out wherever the role is later read.
        var platformPermissionNames = permissionDefinitionService.GetPlatformPermissionNames().ToList();

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
                .Where(permission => !platformPermissionNames.Contains(permission.Name))
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
            // role: a permission the catalogue has since moved to the platform tier, or stopped
            // declaring at all, comes off the role rather than being left behind, so a tenant role can
            // never end up holding a platform-tier permission.
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

        // Removed memberships are soft deleted, so only members who still hold one are counted.
        return await dbContext.TenantMemberships
            .AsNoTracking()
            .AcrossAllTenants()
            .AnyAsync(membership => membership.TenantId == tenantId
                                    && membership.UserId != excludedUserId
                                    && dbContext.UserRoles.Any(assignment => assignment.UserId == membership.UserId
                                                                             && administratorRoleIds.Contains(assignment.RoleId)),
                cancellationToken);
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
        // established and never copied from the session it replaces: the membership, the role
        // assignments and the roles' permissions as they stand at this moment decide it, so authority
        // the caller held in the tenant they acted in a moment ago is not carried into this one.
        var session = await SessionValidator.EvaluateAsync(ReissuePrincipal(account, tenantId), dbContext, cancellationToken);

        var claims = Helper.CreateClaims(account, [.. session.Roles], [.. session.Permissions], tenantId);

        // for cookie authentication
        await CookieAuth.SignInAsync(user => user.Claims.AddRange(claims));

        if (httpContextAccessor.HttpContext is { } httpContext)
        {
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
    /// Builds the principal the re-established session is evaluated as: the account's identity and
    /// the tenant being established, and nothing else, so the evaluation answers what this account
    /// may do in that tenant now rather than echoing the grants the session it replaces carried.
    /// </summary>
    /// <param name="account">The account the session belongs to.</param>
    /// <param name="tenantId">The tenant the session is to act in, or <see langword="null"/> for none.</param>
    /// <returns>A principal carrying identity and tenant claims only.</returns>
    private static ClaimsPrincipal ReissuePrincipal(User account, Guid? tenantId)
        => new(new ClaimsIdentity(Helper.CreateClaims(account, [], [], tenantId), ReissueAuthenticationType));
}