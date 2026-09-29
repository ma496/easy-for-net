namespace Backend.Features.Identity.Endpoints.Account;

using Backend.Features.Tenancy.Core;
using Backend.Features.Identity.Core;

/// <summary>
/// Authenticated GET endpoint that returns the current user's profile information together with the
/// tenants they hold an active membership in, the tenant they are acting in right now, whether their
/// account belongs to the platform tier, and the roles and permissions that tenant grants them. It is the
/// one call the web app makes to learn who the caller is and where they may work, so signing in,
/// reloading a page and switching tenant all read the same answer from the same place.
/// </summary>
/// <remarks>
/// Usable with no tenant established, because a caller with no usable membership - an
/// account created by self-service sign-up that has joined nothing, or a member of several tenants
/// who has not chosen between them yet - has to be able to ask this question: this answer is what
/// tells them they belong to no active tenant, or which tenants they may choose from.
/// </remarks>
sealed class GetInfoEndpoint(AppDbContext dbContext,
                             ICurrentUserService currentUserService,
                             ITenantContext tenantContext)
    : EndpointWithoutRequest<UserGetInfoResponse>
{
    public override void Configure()
    {
        Get("get-info");
        Group<AccountGroup>();
    }

    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetCurrentUserId();
        var user = await dbContext.Users
            .AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => new UserGetInfoResponse
            {
                Id = x.Id,
                Username = x.Username,
                Email = x.Email,
                FirstName = x.FirstName,
                LastName = x.LastName,
                Image = x.Image,
                IsPlatform = x.IsPlatform
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (user is null)
        {
            await Send.NotFoundAsync(cancellationToken);
            return;
        }

        // The tenant being acted in is read from the scope established for this request, which is the
        // tenant the caller's session names and the one every other endpoint will act in for them. It
        // is reported exactly as it stands, so what the web app believes the caller is working in is
        // what the API is actually working in: the two are answered from the same place, and a tenant
        // that has become unusable leaves the session at its next renewal rather than being hidden
        // here while the rest of the API still honours it.
        var scopedTenantId = tenantContext.IsResolved ? tenantContext.CurrentTenantId : null;

        user.Tenants = await TenantsOfAsync(userId, cancellationToken);

        // Read from the list where the caller still holds a membership of the tenant, and from the
        // tenants table otherwise. The two differ once the session's tenant has become unusable - it was
        // suspended, or the caller's membership was removed - while the session is still open: it is
        // reported as it stands so the web app sees the selection is no longer among the ones listed and
        // offers another, rather than believing the caller acts in no tenant while the API still acts in
        // that one. Reading it separately keeps the list's meaning intact: the tenants a caller may select.
        var activeTenant = user.Tenants.FirstOrDefault(tenant => tenant.Id == scopedTenantId);
        if (activeTenant is null && scopedTenantId is { } enteredTenantId)
        {
            activeTenant = await EnteredTenantAsync(enteredTenantId, cancellationToken);
        }

        user.ActiveTenant = activeTenant;
        user.ActiveTenantId = activeTenant?.Id;

        // The grants reported are the session's own - the roles and permissions the API enforces for this
        // very session, read from the session the request was authenticated with rather than worked out
        // again from the database - so what the web app believes the caller may do is what the API will
        // actually allow, including for a session whose role or plan changed after it was minted and has
        // not been renewed. Only the display detail (identifiers, display names, which role grants what)
        // is looked up.
        user.Roles = await RolesOfSessionAsync(scopedTenantId, cancellationToken);

        await Send.ResponseAsync(user, cancellation: cancellationToken);
    }

    /// <summary>
    /// The tenants the caller may work in: those they hold an active membership in that are
    /// themselves active. A suspended or deleted tenant is left out, because switching into one is
    /// refused, so a caller is never offered a choice that cannot be made and a caller left with
    /// none of them is told they belong to no active tenant.
    /// </summary>
    /// <param name="userId">The account whose memberships are read.</param>
    /// <param name="cancellationToken">Token used to cancel the read.</param>
    /// <returns>The tenants the caller holds an active membership in, ordered by name.</returns>
    private async Task<List<UserGetInfoResponse.TenantInfoDto>> TenantsOfAsync(Guid? userId, CancellationToken cancellationToken)
    {
        // Memberships are tenant-restricted themselves, so the restriction is relaxed by name and
        // rewritten as the caller's own predicate: which tenants an account belongs to is a question
        // that cannot be answered from inside one of them, and is asked before any is chosen. The
        // soft-delete filter stays in force, so a membership that was removed places nobody.
        var memberships = dbContext.TenantMemberships
            .AsNoTracking()
            .AcrossAllTenants()
            .Where(membership => membership.UserId == userId);

        // Tenants themselves carry no tenant restriction, so relaxing it here changes nothing about
        // the rows read: it is stated at the root of the query as well because a named filter
        // relaxed there is relaxed for every entity type the query reaches, the membership subquery
        // above included, whichever way the two are composed.
        return await dbContext.Tenants
            .AsNoTracking()
            .AcrossAllTenants()
            .Where(tenant => tenant.Status == TenantStatus.Active
                             && memberships.Any(membership => membership.TenantId == tenant.Id))
            .OrderBy(tenant => tenant.Name)
            .Select(tenant => new UserGetInfoResponse.TenantInfoDto
            {
                Id = tenant.Id,
                Name = tenant.Name,
                Identifier = tenant.Identifier,
                Status = tenant.Status
            })
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// The tenant the caller's session names when it is not among the tenants they may select - their
    /// membership was removed or the tenant suspended after the session was minted. Membership is not
    /// asked about, because the whole point of the read is a caller who no longer holds one, and the
    /// lifecycle is reported rather than judged: the status travels with the tenant so the web app can
    /// say what state it is in.
    /// </summary>
    /// <param name="tenantId">The tenant the request is acting in.</param>
    /// <param name="cancellationToken">Token used to cancel the read.</param>
    /// <returns>The tenant being acted in, or <see langword="null"/> if it cannot be read.</returns>
    private async Task<UserGetInfoResponse.TenantInfoDto?> EnteredTenantAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        // Tenants carry no tenant restriction of their own, but the restriction is relaxed by name all
        // the same, exactly as the read above does it, so the query is unaffected by whichever scope
        // happens to be established. The soft-delete filter stays in force.
        return await dbContext.Tenants
            .AsNoTracking()
            .AcrossAllTenants()
            .Where(tenant => tenant.Id == tenantId)
            .Select(tenant => new UserGetInfoResponse.TenantInfoDto
            {
                Id = tenant.Id,
                Name = tenant.Name,
                Identifier = tenant.Identifier,
                Status = tenant.Status
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// The roles and permissions the caller's session holds, each role with the permissions it grants.
    /// </summary>
    /// <param name="tenantId">The tenant the session acts in, or <see langword="null"/> for none.</param>
    /// <param name="cancellationToken">Token used to cancel the read.</param>
    /// <returns>The session's roles, ordered by name, whose permissions together are exactly the session's.</returns>
    /// <remarks>
    /// The session alone decides what is reported: the role names returned are exactly the session's, and
    /// the permissions across all of them are exactly the session's, whatever has happened to the roles
    /// since it was minted. The database only adds what the session does not store - a role's identifier
    /// and each permission's identifier and display name - and is never used to drop anything. A role that
    /// no longer exists is still reported, by name, with an empty identifier; a permission the database no
    /// longer attributes to any listed role is reported on the first role, and one that no longer exists
    /// carries its name as its display name. Tenant restriction is relaxed by name because the tenant is
    /// stated in the predicate (the platform-scoped roles belong to none); the soft-delete filter stays in
    /// force.
    /// </remarks>
    private async Task<List<UserGetInfoResponse.RoleDto>> RolesOfSessionAsync(Guid? tenantId, CancellationToken cancellationToken)
    {
        var roleNames = currentUserService.GetCurrentUserRoles().Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        var permissionNames = currentUserService.GetCurrentUserPermissions().Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();

        var permissionRows = (await dbContext.Permissions
                .AsNoTracking()
                .Where(permission => permissionNames.Contains(permission.Name))
                .Select(permission => new { permission.Id, permission.Name, permission.DisplayName })
                .ToListAsync(cancellationToken))
            .ToDictionary(permission => permission.Name, StringComparer.Ordinal);

        var roleRows = (await dbContext.Roles
                .AsNoTracking()
                .AcrossAllTenants()
                .Where(role => role.TenantId == tenantId && roleNames.Contains(role.Name))
                .Select(role => new
                {
                    role.Id,
                    role.Name,
                    Permissions = role.RolePermissions.Select(rolePermission => rolePermission.Permission.Name).ToList()
                })
                .ToListAsync(cancellationToken))
            .ToDictionary(role => role.Name, StringComparer.Ordinal);

        UserGetInfoResponse.PermissionDto Describe(string name)
            => permissionRows.TryGetValue(name, out var row)
                ? new() { Id = row.Id, Name = name, DisplayName = row.DisplayName }
                : new() { Id = Guid.Empty, Name = name, DisplayName = name };

        var held = permissionNames.ToHashSet(StringComparer.Ordinal);
        var roles = roleNames
            .Select(name => new UserGetInfoResponse.RoleDto
            {
                Id = roleRows.TryGetValue(name, out var row) ? row.Id : Guid.Empty,
                Name = name,
                Permissions = roleRows.TryGetValue(name, out row)
                    ? [.. row.Permissions.Where(held.Contains).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).Select(Describe)]
                    : []
            })
            .ToList();

        var covered = roles.SelectMany(role => role.Permissions).Select(permission => permission.Name).ToHashSet(StringComparer.Ordinal);
        if (roles.Count > 0)
        {
            roles[0].Permissions.AddRange(permissionNames.Where(name => !covered.Contains(name)).Select(Describe));
        }

        return roles;
    }
}

/// <summary>
/// Response payload for the account info endpoint, exposing the user's identity fields together
/// with the tenants they belong to, the tenant they are acting in, whether their account belongs to
/// the platform tier, and the roles and permissions the active tenant grants them.
/// </summary>
sealed class UserGetInfoResponse
{
    public Guid Id { get; set; }
    public string Username { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Image { get; set; }

    /// <summary>
    /// Gets or sets the tenant the caller is acting in, or <see langword="null"/> when they are
    /// acting in none - because they belong to no active tenant, because they belong to several and
    /// have not chosen, or because the tenant they had chosen stopped being usable.
    /// </summary>
    public Guid? ActiveTenantId { get; set; }

    /// <summary>
    /// Gets or sets the active tenant itself, so the application chrome can name it without looking
    /// it up. It is always one of <see cref="Tenants"/>, and absent whenever
    /// <see cref="ActiveTenantId"/> is.
    /// </summary>
    public TenantInfoDto? ActiveTenant { get; set; }

    /// <summary>
    /// Gets or sets every tenant the caller holds an active membership in. Empty means the caller
    /// belongs to no active tenant; exactly one means there is nothing to choose between; more than
    /// one means the caller picks the tenant to work in before any tenant-scoped screen opens.
    /// </summary>
    public List<TenantInfoDto> Tenants { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the account belongs to the platform tier. It belongs to
    /// no tenant and is therefore neither conferred nor withdrawn by switching between them, which is
    /// what lets the web app offer the way back out of a tenant the account entered.
    /// </summary>
    public bool IsPlatform { get; set; }

    /// <summary>
    /// Gets or sets the roles the caller holds in the active tenant, with the permissions those
    /// roles grant. With no active tenant these are the roles belonging to no tenant.
    /// </summary>
    public List<RoleDto> Roles { get; set; } = [];

    /// <summary>
    /// A tenant the caller may work in, identified well enough for the application chrome to name it
    /// and for a switch to address it.
    /// </summary>
    public sealed class TenantInfoDto : BaseDto<Guid>
    {
        public string Name { get; set; } = null!;
        public string Identifier { get; set; } = null!;
        public TenantStatus Status { get; set; }
    }

    /// <summary>
    /// A role the caller holds, with the permissions it grants.
    /// </summary>
    public sealed class RoleDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
        public List<PermissionDto> Permissions { get; set; } = [];
    }

    /// <summary>
    /// A permission granted by one of the roles the caller holds.
    /// </summary>
    public sealed class PermissionDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
        public string DisplayName { get; set; } = null!;
    }
}
