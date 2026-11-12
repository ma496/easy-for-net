namespace Backend.Features.Identity.Core;

/// <summary>
/// Reads what an account holds - the roles it is assigned and the permissions those roles grant. It
/// is asked once per session rather than once per request: sign-in and a refresh each mint a session
/// that stores the answer, and every request in between is authorized from the claims that stored
/// session is projected onto.
/// </summary>
public static class SessionGrants
{
    /// <summary>
    /// The roles and permissions to mint a session with.
    /// </summary>
    /// <param name="dbContext">The database context the grants are read through.</param>
    /// <param name="userId">The account the session belongs to.</param>
    /// <param name="cancellationToken">Token used to cancel the read.</param>
    /// <returns>The role and permission names the session carries.</returns>
    /// <remarks>
    /// The soft-delete filter stays in force, so a deleted role stops granting what it granted.
    /// </remarks>
    public static async Task<GrantSet> ReadAsync(AppDbContext dbContext,
                                                 Guid userId,
                                                 CancellationToken cancellationToken = default)
    {
        var grants = await dbContext.Roles
            .AsNoTracking()
            .Where(role => dbContext.UserRoles.Any(assignment => assignment.UserId == userId && assignment.RoleId == role.Id))
            .Select(role => new
            {
                role.Name,
                Permissions = role.RolePermissions
                    .Select(rolePermission => rolePermission.Permission.Name)
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        return new GrantSet
        {
            Roles = [.. grants.Select(grant => grant.Name).Distinct(StringComparer.Ordinal)],
            Permissions = [.. grants.SelectMany(grant => grant.Permissions).Distinct(StringComparer.Ordinal)]
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
    /// Gets the names of the roles the account holds.
    /// </summary>
    public List<string> Roles { get; init; } = [];

    /// <summary>
    /// Gets the names of the permissions those roles grant, without duplicates.
    /// </summary>
    public List<string> Permissions { get; init; } = [];
}
