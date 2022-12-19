using Ardalis.GuardClauses;
using EasyForNet.Entities;
using EasyForNet.EntityFramework.Data.Context;
using EasyForNet.Helpers;
using EasyForNet.Key;
using EasyForNet.Setting;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace EasyForNet.EntityFramework.Setting;

public class EfSettingStore<TEntity> : ISettingStore<TEntity>
    where TEntity : EfnSetting
{
    private readonly EfnDbContext _dbContext;
    private readonly IKeyManager _keyManager;

    protected DbSet<TEntity> DbSet { get; }

    public EfSettingStore(EfnDbContext dbContext, IKeyManager keyManager)
    {
        DbSet = dbContext.Set<TEntity>();
        _dbContext = dbContext;
        _keyManager = keyManager;
    }

    public virtual TValue Get<TValue>(string key)
    {
        Guard.Against.NullOrWhiteSpace(key, nameof(key));

        var setting = DbSet.Where(e => e.Key.Equals(_keyManager.GlobalKey(key)))
            .SingleOrDefault();

        if (setting == null || setting.Value == null)
            return default(TValue);

        return JsonHelper.Deserialize<TValue>(setting.Value);
    }

    public virtual async Task<TValue> GetAsync<TValue>(string key)
    {
        Guard.Against.NullOrWhiteSpace(key, nameof(key));

        var setting = await DbSet.Where(e => e.Key.Equals(_keyManager.GlobalKey(key)))
            .SingleOrDefaultAsync();

        if (setting == null || setting.Value == null)
            return default(TValue);

        return JsonHelper.Deserialize<TValue>(setting.Value);
    }

    public void Set<TValue>(string key, TValue value)
    {
        Guard.Against.NullOrWhiteSpace(key, nameof(key));
        Guard.Against.Null(value, nameof(value));

        var globalKey = _keyManager.GlobalKey(key);
        var jsonValue = JsonHelper.ToJson(value);
        var setting = DbSet.Where(e => e.Key.Equals(globalKey))
            .SingleOrDefault();

        if (setting == null)
            DbSet.Add((TEntity)new EfnSetting { Key = globalKey, Value = jsonValue });
        else
        {
            setting.Value = jsonValue;
        }

        _dbContext.SaveChanges();
    }

    public async Task SetAsync<TValue>(string key, TValue value)
    {
        Guard.Against.NullOrWhiteSpace(key, nameof(key));
        Guard.Against.Null(value, nameof(value));

        var globalKey = _keyManager.GlobalKey(key);
        var jsonValue = JsonHelper.ToJson(value);
        var setting = await DbSet.Where(e => e.Key.Equals(globalKey))
            .SingleOrDefaultAsync();

        if (setting == null)
            DbSet.Add((TEntity)new EfnSetting { Key = globalKey, Value = jsonValue });
        else
        {
            setting.Value = jsonValue;
        }

        await _dbContext.SaveChangesAsync();
    }

    public IQueryable<TEntity> GetAll()
    {
        return DbSet;
    }
}