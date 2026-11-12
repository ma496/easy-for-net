using Backend.Attributes;
using Backend.Features.Identity.Core.Entities;

namespace Backend.Features.Identity.Core;

/// <summary>
/// Defines CRUD and lookup operations for <see cref="User"/> entities, including password management and role assignment.
/// </summary>
public interface IUserService
{
    Task<User?> GetByIdAsync(Guid id);
    Task<User?> GetByUsernameAsync(string username);
    Task<User?> GetByEmailAsync(string email);

    /// <summary>
    /// Every user account. Lists, searches and counts all narrow from this one query.
    /// </summary>
    /// <returns>A composable query over every account.</returns>
    IQueryable<User> Users();

    /// <summary>
    /// Creates an account with the supplied password.
    /// </summary>
    /// <param name="user">The account to create, carrying any role assignments it is to start with.</param>
    /// <param name="password">The initial password, hashed before it is stored.</param>
    /// <returns>The created account.</returns>
    Task<User> CreateAsync(User user, string password);

    Task UpdateAsync(User user);
    Task DeleteAsync(Guid id);
    Task DeleteAsync(User user);
    Task<bool> ValidatePasswordAsync(User user, string password);

    Task AssignRoleAsync(Guid userId, Guid roleId);
    Task RemoveRoleAsync(Guid userId, Guid roleId);
    Task<bool> IsInRoleAsync(Guid userId, Guid roleId);
    Task<User> UpdatePasswordAsync(User user, string password);
    Task UpdateLastSigninAsync(Guid userId);
}

/// <summary>
/// EF Core-backed implementation of <see cref="IUserService"/> that manages users, their passwords, and role memberships.
/// </summary>
[NoDirectUse]
public class UserService(AppDbContext dbContext,
                         IPasswordHasher passwordHasher) : IUserService
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

    /// <inheritdoc />
    public IQueryable<User> Users()
    {
        return dbContext.Users;
    }

    /// <inheritdoc />
    public async Task<User> CreateAsync(User user, string password)
    {
        user.PasswordHash = passwordHasher.HashPassword(password);
        dbContext.Users.Add(user);


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
        user.SecurityStamp = Guid.NewGuid();
        await UpdateAsync(user);
        return user;
    }

    public async Task UpdateLastSigninAsync(Guid userId)
    {
        await dbContext.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(set => set.SetProperty(u => u.LastSigninAt, DateTime.UtcNow));
    }
}
