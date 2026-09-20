namespace Backend.Features.Identity.Endpoints.Account;

using Backend.Data.Entities;
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

        // Read from the list where the caller holds a membership of the tenant, and from the tenants
        // table otherwise. The two differ for a platform account by design: it enters a tenant on its
        // tier and holds no membership in it, so that tenant is never among the ones listed above and
        // would otherwise be reported as no active tenant at all - leaving the web app to send it
        // straight back out of the tenant it just entered. Reading it separately keeps the list's
        // meaning intact: the tenants a caller may select by membership.
        var activeTenant = user.Tenants.FirstOrDefault(tenant => tenant.Id == scopedTenantId);
        if (activeTenant is null && scopedTenantId is { } enteredTenantId)
        {
            activeTenant = await EnteredTenantAsync(enteredTenantId, cancellationToken);
        }

        user.ActiveTenant = activeTenant;
        user.ActiveTenantId = activeTenant?.Id;

        // The grants reported are those of the tenant just reported as active, together with the
        // caller's platform-scoped roles - exactly the set a session acting in that tenant is minted
        // with, so what the web app believes the caller may do matches what the API will actually
        // allow. Authority held in the tenant left behind by a switch is not among them; a
        // platform-scoped role is, because it belongs to no tenant and survives every switch. With no
        // active tenant only the platform-scoped roles remain, which for an ordinary account is an
        // empty list.
        user.Roles = await RolesInAsync(userId, activeTenant?.Id, user.IsPlatform, cancellationToken);

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
    /// The tenant the caller's session names when it is not one they hold a membership of - which is
    /// what a platform account entering a tenant looks like. Membership is not asked about, because
    /// the whole point of the read is a caller who holds none, and the lifecycle is reported rather
    /// than judged: the status travels with the tenant so the web app can say what state it is in.
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
    /// The roles the caller holds inside one named tenant, together with the platform-scoped roles it
    /// holds in every tenant, each with the permissions it grants.
    /// </summary>
    /// <param name="userId">The account whose role assignments are read.</param>
    /// <param name="tenantId">
    /// The tenant the roles are read for, or <see langword="null"/> for the caller's platform-scoped
    /// roles - the ones belonging to no tenant.
    /// </param>
    /// <param name="isPlatform">Whether the account belongs to the platform tier.</param>
    /// <param name="cancellationToken">Token used to cancel the read.</param>
    /// <returns>The roles held there, ordered by name, each with the permissions it grants.</returns>
    /// <remarks>
    /// The tenant is stated in the predicate rather than left to the query filter, because the same
    /// read has to answer for the roles belonging to no tenant as well as for one tenant's own.
    /// Relaxing tenant restriction by name leaves the soft-delete filter applied, so a deleted role
    /// stops granting what it granted.
    /// <para>
    /// The platform-scoped roles are included whichever tenant is named, because that is how a session
    /// is minted: a platform-scoped role's permissions are granted in every tenant, narrowed to the
    /// scope being acted in. Leaving them out here would hide a platform account's own permissions
    /// from the web app the moment it started working inside one of its tenants, and the screens those
    /// permissions unlock would be refused by a client that the API would have admitted.
    /// </para>
    /// <para>
    /// The narrowing is <see cref="SessionGrants"/>' own, asked for here rather than restated, so what
    /// the web app believes the caller may do is what the API will actually allow: the tenant tier
    /// inside a tenant, the platform tier in platform scope, and nothing at all for an ordinary account
    /// that has no tenant active. Only the projection differs - this one carries ids and display names
    /// for the screens to read, where a session carries names alone.
    /// </para>
    /// </remarks>
    private async Task<List<UserGetInfoResponse.RoleDto>> RolesInAsync(Guid? userId, Guid? tenantId, bool isPlatform, CancellationToken cancellationToken)
    {
        var viewScope = SessionGrants.ScopeOf(tenantId);
        var exercisesPermissions = SessionGrants.ExercisesPermissions(isPlatform, tenantId);

        return await dbContext.Roles
            .AsNoTracking()
            .AcrossAllTenants()
            .Where(role => (role.TenantId == tenantId || role.TenantId == null)
                           && dbContext.UserRoles.Any(assignment => assignment.UserId == userId && assignment.RoleId == role.Id))
            .OrderBy(role => role.Name)
            .Select(role => new UserGetInfoResponse.RoleDto
            {
                Id = role.Id,
                Name = role.Name,
                Permissions = role.RolePermissions
                    .Where(rolePermission => exercisesPermissions
                                             && (rolePermission.Permission.Scope == viewScope
                                                 || rolePermission.Permission.Scope == PermissionScope.Both))
                    .Select(rolePermission => new UserGetInfoResponse.PermissionDto
                    {
                        Id = rolePermission.PermissionId,
                        Name = rolePermission.Permission.Name,
                        DisplayName = rolePermission.Permission.DisplayName
                    }).ToList()
            })
            .ToListAsync(cancellationToken);
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
