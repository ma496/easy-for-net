using Ardalis.GuardClauses;
using CSharpTemplate.Common.Identity.Dto;
using CSharpTemplate.Common.Identity.Entities;
using EasyForNet.Cache;
using EasyForNet.Domain.Services;
using EasyForNet.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace CSharpTemplate.Common.Identity.Permissions;

public class PermissionManager : DomainService, IPermissionManager
{
    private const string KeyPrefix = "Permissions";
    private readonly ICacheManager _cacheManager;
    private readonly IRepository<User, long> _userRepository;

    public PermissionManager(ICacheManager cacheManager, IRepository<User, long> userRepository)
    {
        _cacheManager = cacheManager;
        _userRepository = userRepository;
    }

    public async Task SetPermissions(long userId)
    {
        var user = await _userRepository.GetAll()
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .ThenInclude(r => r.RolePermissions)
            .ThenInclude(rp => rp.Permission)
            .Where(u => u.Id == userId)
            .SingleOrDefaultAsync();

        Guard.Against.Null(user, nameof(user));

        var permissions = new HashSet<string>();
        foreach (var ur in user.UserRoles)
        {
            foreach (var rp in ur.Role.RolePermissions)
            {
                if (!permissions.Contains(rp.Permission.Name))
                    permissions.Add(rp.Permission.Name);
            }
        }

        var key = GetKey(userId);
        var cache = new PermissionsCacheModel
        {
            Permissions = permissions
        };
        await _cacheManager.SetAsync(key, cache, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(10000)
        });
    }

    public async Task<PermissionsCacheModel> GetPermissions(long userId)
    {
        var key = GetKey(userId);
        return await _cacheManager.GetAsync<PermissionsCacheModel>(key);
    }

    private string GetKey(long userId)
    {
        return $"{KeyPrefix}_{userId}";
    }
}