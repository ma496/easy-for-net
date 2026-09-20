namespace Backend.Features.Identity.Core;

/// <summary>
/// Reads what an account holds inside one tenant - the roles it is assigned there and the
/// permissions those roles grant, narrowed to the scope the session will act in. It is asked once
/// per session rather than once per request: sign-in, a refresh and a tenant switch each mint a
/// token carrying the answer, and every request in between is authorized from the claims that token
/// already holds.
/// </summary>
/// <remarks>
/// The narrowing is stated here and nowhere else, so the claims a session is minted with and the
/// grants the account info endpoint reports cannot describe different authority for the same caller.
/// </remarks>
public static class SessionGrants
{
    /// <summary>
    /// The permission tier a session acting in this tenant exercises: the tenant tier inside one, and
    /// the platform tier in none. A permission declared for both is held either way, which is the whole
    /// of the rule that hands a platform account a tenant's own authority when it enters one and gives
    /// its platform authority back when it leaves.
    /// </summary>
    /// <param name="tenantId">The tenant the session acts in, or <see langword="null"/> for none.</param>
    /// <returns>The scope whose permissions the session may exercise.</returns>
    public static PermissionScope ScopeOf(Guid? tenantId)
        => tenantId is null ? PermissionScope.Platform : PermissionScope.Tenant;

    /// <summary>
    /// Whether the account exercises any permission at all in that scope. The platform scope belongs
    /// to platform accounts alone: an ordinary account acting in no tenant has not chosen one yet, or
    /// has lost the one it had, and is not thereby working on the platform. It keeps its role names,
    /// which describe who it is rather than what it may do here.
    /// </summary>
    /// <param name="isPlatform">Whether the account belongs to the platform tier.</param>
    /// <param name="tenantId">The tenant the session acts in, or <see langword="null"/> for none.</param>
    /// <returns><see langword="true"/> when the session exercises the permissions its roles grant.</returns>
    public static bool ExercisesPermissions(bool isPlatform, Guid? tenantId)
        => isPlatform || tenantId is not null;

    /// <summary>
    /// The roles and permissions to mint a session with: those granted inside the tenant being acted
    /// in, plus the account's platform-scoped roles, which belong to no tenant and are therefore
    /// neither conferred nor withdrawn by one.
    /// </summary>
    /// <param name="dbContext">The database context the grants are read through.</param>
    /// <param name="userId">The account the session belongs to.</param>
    /// <param name="tenantId">The tenant the session acts in, or <see langword="null"/> for none.</param>
    /// <param name="isPlatform">Whether the account belongs to the platform tier.</param>
    /// <param name="cancellationToken">Token used to cancel the read.</param>
    /// <returns>The role and permission names the session carries.</returns>
    /// <remarks>
    /// Tenant restriction is relaxed by name because this runs before any tenant scope is established -
    /// at sign-in and at refresh there is no scope at all, and the tenant being read for is stated in
    /// the predicate instead. The soft-delete filter stays in force throughout, so a deleted role stops
    /// granting what it granted.
    /// </remarks>
    public static async Task<GrantSet> ReadAsync(AppDbContext dbContext,
                                                 Guid userId,
                                                 Guid? tenantId,
                                                 bool isPlatform,
                                                 CancellationToken cancellationToken = default)
    {
        var viewScope = ScopeOf(tenantId);

        var grants = await dbContext.Roles
            .AsNoTracking()
            .AcrossAllTenants()
            .Where(role => (role.TenantId == null || role.TenantId == tenantId)
                           && dbContext.UserRoles.Any(assignment => assignment.UserId == userId && assignment.RoleId == role.Id))
            .Select(role => new
            {
                role.Name,
                Permissions = role.RolePermissions
                    .Where(rolePermission => rolePermission.Permission.Scope == viewScope
                                             || rolePermission.Permission.Scope == PermissionScope.Both)
                    .Select(rolePermission => rolePermission.Permission.Name)
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        return new GrantSet
        {
            Roles = [.. grants.Select(grant => grant.Name).Distinct(StringComparer.Ordinal)],
            Permissions = ExercisesPermissions(isPlatform, tenantId)
                ? [.. grants.SelectMany(grant => grant.Permissions).Distinct(StringComparer.Ordinal)]
                : []
        };
    }
}

/// <summary>
/// The roles and permissions one session is minted with. It describes the account's standing at the
/// moment the token was issued, which is what every request that token authorizes is decided on.
/// </summary>
public sealed class GrantSet
{
    /// <summary>
    /// Gets the names of the roles the account holds: those granted inside the tenant being acted in,
    /// plus its platform-scoped roles.
    /// </summary>
    public List<string> Roles { get; init; } = [];

    /// <summary>
    /// Gets the names of the permissions those roles grant, narrowed to the scope being acted in and
    /// without duplicates. Empty for an ordinary account acting in no tenant.
    /// </summary>
    public List<string> Permissions { get; init; } = [];
}
