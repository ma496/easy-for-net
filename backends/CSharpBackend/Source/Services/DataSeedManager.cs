using Efn.Identity.Entities;
using Efn.Identity.Services;
using Efn.Infrastructure.EfPersistence;
using Microsoft.EntityFrameworkCore;

namespace Efn.Services;

public class DataSeedManager : IDataSeedManager
{
    private readonly AppDbContext _appDbContext;
    private readonly IUserManager _userManager;

    public DataSeedManager(AppDbContext appDbContext, IUserManager userManager)
    {
        _appDbContext = appDbContext;
        _userManager = userManager;
    }

    public async Task Seed()
    {
        var user = new User
        {
            Name = "Admin",
            Email = "admin@gmail.com",
            Username = "admin",
            Password = "Admin@123",
        };

        if (!await _appDbContext.Users.AnyAsync(u => u.Username == user.Username))
            await _userManager.Create(user);
    }
}

public interface IDataSeedManager
{
    Task Seed();
}
