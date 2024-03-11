using Efn.Identity.Entities;
using Efn.Infrastructure.EfPersistence;

namespace Efn.Identity.Services;

public class UserManager : IUserManager
{
    readonly AppDbContext _dbContext;

    public UserManager(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Create(User user)
    {
        // TODO convert password to hash
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();
    }
}

public interface IUserManager
{ 
    Task Create(User user);
}
