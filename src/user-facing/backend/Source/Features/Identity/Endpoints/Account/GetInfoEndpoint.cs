using Backend.Features.Identity.Core;

namespace Backend.Features.Identity.Endpoints.Account;

/// <summary>
/// Authenticated GET endpoint that returns the current user's profile information together with the
/// roles they hold and the permissions those roles grant. It is the one call the web app makes to learn
/// who the caller is and what they may do.
/// </summary>
sealed class GetInfoEndpoint(AppDbContext dbContext,
                             ICurrentUserService currentUserService)
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
                Image = x.Image
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (user is null)
        {
            await Send.NotFoundAsync(cancellationToken);
            return;
        }

        // The grants reported are the session's own - the roles and permissions the API enforces for this
        // very session, read from the session the request was authenticated with rather than worked out
        // again from the database - so what the web app believes the caller may do is what the API will
        // actually allow, including for a session whose role or plan changed after it was minted and has
        // not been renewed. Only the display detail (identifiers, display names, which role grants what)
        // is looked up.
        user.Roles = await RolesOfSessionAsync(cancellationToken);

        await Send.ResponseAsync(user, cancellation: cancellationToken);
    }

    /// <summary>
    /// The roles and permissions the caller's session holds, each role with the permissions it grants.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the read.</param>
    /// <returns>The session's roles, ordered by name, whose permissions together are exactly the session's.</returns>
    /// <remarks>
    /// The session alone decides what is reported: the role names returned are exactly the session's, and
    /// the permissions across all of them are exactly the session's, whatever has happened to the roles
    /// since it was minted. The database only adds what the session does not store - a role's identifier
    /// and each permission's identifier and display name - and is never used to drop anything. A role that
    /// no longer exists is still reported, by name, with an empty identifier; a permission the database no
    /// longer attributes to any listed role is reported on the first role, and one that no longer exists
    /// carries its name as its display name. The soft-delete filter stays in force.
    /// </remarks>
    private async Task<List<UserGetInfoResponse.RoleDto>> RolesOfSessionAsync(CancellationToken cancellationToken)
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
                .Where(role => roleNames.Contains(role.Name))
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
/// with the roles they hold and the permissions those roles grant.
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
    /// Gets or sets the roles the caller holds, with the permissions those roles grant.
    /// </summary>
    public List<RoleDto> Roles { get; set; } = [];

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
