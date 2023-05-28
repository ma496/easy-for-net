using Ardalis.GuardClauses;
using EasyForNet.Entities;
using EasyForNet.EntityFramework.Data.Context;
using EasyForNet.Helpers;
using EasyForNet.Setting;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace EasyForNet.EntityFramework.Setting;

public class EfSettingStore<TEntity> : ISettingStore<TEntity>
    where TEntity : EfnSetting
{
    private readonly EfnDbContext _dbContext;

    protected DbSet<TEntity> DbSet { get; }

    public EfSettingStore(EfnDbContext dbContext)
    {
        DbSet = dbContext.Set<TEntity>();
        _dbContext = dbContext;
    }

    public virtual TValue Get<TValue>(string key)
    {
        Guard.Against.NullOrWhiteSpace(key, nameof(key));

        var setting = DbSet.SingleOrDefault(e => e.Key.Equals(key));

        if (setting == null || setting.Value == null)
            return default;

        return JsonHelper.Deserialize<TValue>(setting.Value);
    }

    public virtual async Task<TValue> GetAsync<TValue>(string key)
    {
        Guard.Against.NullOrWhiteSpace(key, nameof(key));

        var setting = await DbSet.Where(e => e.Key.Equals(key))
            .SingleOrDefaultAsync();

        if (setting == null || setting.Value == null)
            return default(TValue);

        return JsonHelper.Deserialize<TValue>(setting.Value);
    }

    public void Set<TValue>(string key, TValue value)
    {
        Guard.Against.NullOrWhiteSpace(key, nameof(key));
        Guard.Against.Null(value, nameof(value));

        var jsonValue = JsonHelper.ToJson(value);
        var setting = DbSet.SingleOrDefault(e => e.Key.Equals(key));

        if (setting == null)
            DbSet.Add((TEntity)new EfnSetting { Key = key, Value = jsonValue });
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

        var jsonValue = JsonHelper.ToJson(value);
        var setting = await DbSet.Where(e => e.Key.Equals(key))
            .SingleOrDefaultAsync();

        if (setting == null)
            DbSet.Add((TEntity)new EfnSetting { Key = key, Value = jsonValue });
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