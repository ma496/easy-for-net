using Ardalis.GuardClauses;
using EasyForNet.EntityFramework.Data.Context;
using EasyForNet.EntityFramework.Data.Entities;
using EasyForNet.Helpers;
using EasyForNet.Key;
using EasyForNet.Setting;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EasyForNet.EntityFramework.Setting
{
    public class SettingStore<TDbContext, TEntity> : ISettingStore
        where TDbContext : DbContextBase
        where TEntity : EfnSettingEntity
    {
        private readonly TDbContext _dbContext;
        private readonly IKeyManager _keyManager;

        protected DbSet<TEntity> DbSet { get; }

        public SettingStore(TDbContext dbContext, IKeyManager keyManager)
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

            var globalKey = _keyManager.GlobalKey(key);
            var jsonValue = JsonHelper.ToJson(value);
            var setting = DbSet.Where(e => e.Key.Equals(globalKey))
                .SingleOrDefault();

            if (setting == null)
                DbSet.Add((TEntity)new EfnSettingEntity { Key = globalKey, Value = jsonValue });
            else
            {
                setting.Value = jsonValue;
            }

            SaveChanges();
        }

        public async Task SetAsync<TValue>(string key, TValue value)
        {
            Guard.Against.NullOrWhiteSpace(key, nameof(key));

            var globalKey = _keyManager.GlobalKey(key);
            var jsonValue = JsonHelper.ToJson(value);
            var setting = await DbSet.Where(e => e.Key.Equals(globalKey))
                .SingleOrDefaultAsync();

            if (setting == null)
                DbSet.Add((TEntity)new EfnSettingEntity { Key = globalKey, Value = jsonValue });
            else
            {
                setting.Value = jsonValue;
            }

            await SaveChangesAsync();
        }

        public Dictionary<string, string> GetAll()
        {
            return DbSet.ToDictionary(e => e.Key, e => e.Value);
        }

        public async Task<Dictionary<string, string>> GetAllAsync()
        {
            return await DbSet.ToDictionaryAsync(e => e.Key, e => e.Value);
        }

        protected void SaveChanges()
        {
            _dbContext.SaveChanges();
        }

        protected async Task SaveChangesAsync()
        {
            await _dbContext.SaveChangesAsync();
        }
    }
}
