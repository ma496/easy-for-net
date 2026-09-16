namespace Backend.Features.Identity.Core;

using Backend.Attributes;
using Backend.Data.Entities;
using Backend.Features.Identity.Core.Entities;

/// <summary>
/// Defines CRUD and lookup operations for <see cref="User"/> entities, including password management and role assignment.
/// </summary>
/// <remarks>
/// An account belongs to no tenant of its own - one account is one identity across every tenant - so the lookups
/// by identifier, username and email answer platform-wide, which is what lets a caller sign in before any tenant
/// has been established. What a tenant scopes is everything hung off the account: the roles and permissions it
/// holds are read for one named tenant and no other, and the accounts a caller may administer are the ones holding
/// an active membership of the tenant being acted in.
/// </remarks>
public interface IUserService
{
    Task<User?> GetByIdAsync(Guid id);
    Task<User?> GetByUsernameAsync(string username);
    Task<User?> GetByEmailAsync(string email);
    Task<User?> GetByEmailOrUsernameAsync(string emailOrUsername);

    /// <summary>
    /// Every user account, restricted by no tenant. This is the platform-wide set, and the set a
    /// platform administrator is entitled to; a caller administering accounts from inside a tenant
    /// reads <see cref="TenantUsers"/> instead.
    /// </summary>
    /// <returns>A composable query over every account.</returns>
    IQueryable<User> Users();

    /// <summary>
    /// The accounts the caller may administer right now: those holding an active membership of the
    /// tenant being acted in, widened to every account when the caller holds platform administration.
    /// Lists, searches and counts all narrow from this one query, so none of them can forget the
    /// restriction and none of them can disagree about it.
    /// </summary>
    /// <returns>A composable query over the accounts the caller may administer.</returns>
    /// <remarks>
    /// Acting in no tenant is not an error here, it is an empty set: a membership always names a
    /// tenant, so a caller whose scope is the platform rather than a tenant holds no accounts to
    /// administer and reads none. A caller with no scope established at all - work outside a request
    /// that never named the tenant it acts for - fails with
    /// <see cref="TenantScopeNotEstablishedException"/> rather than quietly reading nothing.
    /// </remarks>
    IQueryable<User> TenantUsers();

    /// <summary>
    /// Creates an account with the supplied password and, when the caller is acting inside a tenant,
    /// grants the new account an active membership of that tenant in the same transaction, so an
    /// administrator never creates an account they cannot then see.
    /// </summary>
    /// <param name="user">The account to create, carrying any role assignments it is to start with.</param>
    /// <param name="password">The initial password, hashed before it is stored.</param>
    /// <returns>The created account.</returns>
    /// <remarks>
    /// No membership is written when the caller is acting in no tenant: self-service sign-up creates
    /// a global account that joins nothing, and the seeder runs before any tenant exists at all.
    /// </remarks>
    Task<User> CreateAsync(User user, string password);

    Task UpdateAsync(User user);
    Task DeleteAsync(Guid id);
    Task DeleteAsync(User user);
    Task<bool> ValidatePasswordAsync(User user, string password);

    /// <summary>
    /// The names of the roles the account holds in one named tenant, and in no other.
    /// </summary>
    /// <param name="userId">The account whose roles are read.</param>
    /// <param name="tenantId">
    /// The tenant the roles are read for, or <see langword="null"/> for the account's platform-scoped
    /// roles - the ones belonging to no tenant, which for an ordinary account is nothing at all.
    /// </param>
    /// <returns>The names of the roles held there.</returns>
    Task<List<string>> GetUserRolesAsync(Guid userId, Guid? tenantId = null);

    /// <summary>
    /// The names of the permissions the account's roles grant it in one named tenant, without
    /// duplicates. Only that tenant's roles are read, so authority held in one tenant confers nothing
    /// in another and what a session may do is decided by the tenant it is acting in alone.
    /// </summary>
    /// <param name="userId">The account whose permissions are read.</param>
    /// <param name="tenantId">
    /// The tenant the permissions are read for, or <see langword="null"/> for those its platform-scoped
    /// roles grant.
    /// </param>
    /// <returns>The distinct names of the permissions granted there.</returns>
    Task<List<string>> GetUserPermissionsAsync(Guid userId, Guid? tenantId = null);

    Task AssignRoleAsync(Guid userId, Guid roleId);
    Task RemoveRoleAsync(Guid userId, Guid roleId);
    Task<bool> IsInRoleAsync(Guid userId, Guid roleId);
    Task<User> UpdatePasswordAsync(User user, string password);
    Task UpdateLastSigninAsync(Guid userId);
}

/// <summary>
/// EF Core-backed implementation of <see cref="IUserService"/> that manages users, their passwords, and role memberships.
/// </summary>
/// <remarks>
/// Every read here that concerns a tenant relaxes tenant restriction by name and states the tenant it
/// means in its own predicate, because the tenant asked about is not always the one being acted in -
/// sign-in resolves a session's roles before any scope exists, and a platform administrator reads
/// accounts from outside every tenant. The soft-delete filter stays in force throughout, so a deleted
/// role grants nothing and a removed membership places nobody.
/// </remarks>
[NoDirectUse]
public class UserService(AppDbContext dbContext,
                         IPasswordHasher passwordHasher,
                         ITenantContext tenantContext,
                         ICurrentUserService currentUserService) : IUserService
{
    public async Task<User?> GetByIdAsync(Guid id)
    {
        return await dbContext.Users.FindAsync(id);
    }

    public async Task<User?> GetByUsernameAsync(string username)
    {
        return await dbContext.Users.FirstOrDefaultAsync(u => u.UsernameNormalized == username.ToLowerInvariant());
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        return await dbContext.Users.FirstOrDefaultAsync(u => u.EmailNormalized == email.ToLowerInvariant());
    }

    public async Task<User?> GetByEmailOrUsernameAsync(string emailOrUsername)
    {
        return await dbContext.Users.FirstOrDefaultAsync(u => u.EmailNormalized == emailOrUsername.ToLowerInvariant() || u.UsernameNormalized == emailOrUsername.ToLowerInvariant());
    }

    /// <inheritdoc />
    public IQueryable<User> Users()
    {
        return dbContext.Users;
    }

    /// <inheritdoc />
    public IQueryable<User> TenantUsers()
    {
        // Platform administration is the tier test, and it is read from the request's live permission
        // claims, which the session check has already recomputed from current data. A holder of it
        // administers accounts irrespective of membership, so the set is not narrowed at all for them.
        if (currentUserService.HasPermission(Allow.Platform_Administration))
        {
            return Users();
        }

        // Read once, outside the expression, so the tenant the restriction means is fixed here rather
        // than re-read while the query is translated - and so a scope that was never established fails
        // at the call rather than somewhere inside a deferred query.
        var activeTenantId = tenantContext.CurrentTenantId;

        // Memberships are themselves tenant-restricted, but the restriction is relaxed and rewritten
        // as an explicit predicate: this reads exactly the same way for the tenant being acted in and
        // for platform scope, where it names no tenant and therefore matches no membership at all.
        var memberships = dbContext.TenantMemberships
            .AcrossAllTenants()
            .Where(membership => membership.TenantId == activeTenantId);

        return Users().Where(account => memberships.Any(membership => membership.UserId == account.Id));
    }

    /// <inheritdoc />
    public async Task<User> CreateAsync(User user, string password)
    {
        user.PasswordHash = passwordHasher.HashPassword(password);
        dbContext.Users.Add(user);

        // A tenant is established for a request made from inside one, resolved to the platform for the
        // anonymous and self-service flows, and not established at all for the seeder, which runs
        // before any request. Only the first of those grants a membership, and asking whether a scope
        // exists before reading it is what keeps the other two working rather than failing.
        if (tenantContext.IsResolved && tenantContext.CurrentTenantId is { } activeTenantId)
        {
            // The account's key is generated when it is added rather than when it is saved, so the
            // membership can name it and both rows land in one transaction: an account is never left
            // behind without the membership that makes its creator able to see it.
            dbContext.TenantMemberships.Add(new TenantMembership { TenantId = activeTenantId, UserId = user.Id });
        }

        await dbContext.SaveChangesAsync();
        return user;
    }

    public async Task UpdateAsync(User user)
    {
        dbContext.Users.Update(user);
        await dbContext.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var user = await GetByIdAsync(id);
        if (user != null)
        {
            dbContext.Users.Remove(user);
            await dbContext.SaveChangesAsync();
        }
    }

    public async Task DeleteAsync(User user)
    {
        dbContext.Users.Remove(user);
        await dbContext.SaveChangesAsync();
    }

    public async Task<bool> ValidatePasswordAsync(User user, string password)
    {
        var isValid = passwordHasher.VerifyPassword(user.PasswordHash, password);
        if (isValid && passwordHasher.NeedsRehash(user.PasswordHash))
        {
            user.PasswordHash = passwordHasher.HashPassword(password);
            await dbContext.SaveChangesAsync();
        }

        return isValid;
    }

    /// <inheritdoc />
    public async Task<List<string>> GetUserRolesAsync(Guid userId, Guid? tenantId = null)
    {
        return await RolesHeldIn(userId, tenantId)
            .Select(role => role.Name)
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<List<string>> GetUserPermissionsAsync(Guid userId, Guid? tenantId = null)
    {
        return await RolesHeldIn(userId, tenantId)
            .SelectMany(role => role.RolePermissions)
            .Select(rolePermission => rolePermission.Permission.Name)
            .Distinct()
            .ToListAsync();
    }

    public async Task AssignRoleAsync(Guid userId, Guid roleId)
    {
        var userRole = new UserRole { UserId = userId, RoleId = roleId };
        dbContext.UserRoles.Add(userRole);
        await dbContext.SaveChangesAsync();
    }

    public async Task RemoveRoleAsync(Guid userId, Guid roleId)
    {
        var userRole = await dbContext.UserRoles
            .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId);
        if (userRole != null)
        {
            dbContext.UserRoles.Remove(userRole);
            await dbContext.SaveChangesAsync();
        }
    }

    public async Task<bool> IsInRoleAsync(Guid userId, Guid roleId)
    {
        return await dbContext.UserRoles.AnyAsync(ur => ur.UserId == userId && ur.RoleId == roleId);
    }

    public async Task<User> UpdatePasswordAsync(User user, string password)
    {
        user.PasswordHash = passwordHasher.HashPassword(password);
        await UpdateAsync(user);
        return user;
    }

    public async Task UpdateLastSigninAsync(Guid userId)
    {
        await dbContext.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(set => set.SetProperty(u => u.LastSigninAt, DateTime.UtcNow));
    }

    /// <summary>
    /// The roles an account holds inside one tenant: the roles of that tenant alone that the account
    /// is assigned. This is the single predicate both grant readings above are built from, so the
    /// permissions a request is evaluated against can never come from a wider set of roles than the
    /// roles the same tenant reports.
    /// </summary>
    /// <param name="userId">The account whose assignments are read.</param>
    /// <param name="tenantId">The tenant the roles belong to, or <see langword="null"/> for the roles belonging to none.</param>
    /// <returns>A query over the roles held there.</returns>
    /// <remarks>
    /// The tenant is stated in the predicate rather than left to the query filter, because the tenant
    /// asked about is not always the one being acted in and is often asked for before any scope exists
    /// at all - which is why the restriction is relaxed by name first. The soft-delete filter is
    /// untouched by that, so a deleted role stops granting what it granted.
    /// </remarks>
    private IQueryable<Role> RolesHeldIn(Guid userId, Guid? tenantId)
    {
        return dbContext.Roles
            .AsNoTracking()
            .AcrossAllTenants()
            .Where(role => role.TenantId == tenantId
                           && dbContext.UserRoles.Any(assignment => assignment.UserId == userId && assignment.RoleId == role.Id));
    }
}
