using Ardalis.GuardClauses;
using EasyForNet.Application.Dependencies;
using EasyForNet.Cache;
using EasyForNet.Helpers;
using Microsoft.Extensions.Caching.Distributed;
using System;
using System.Threading.Tasks;

namespace EasyForNet.Setting
{
    public interface ISettingManager
    {
        TValue Get<TValue>(string key);
        Task<TValue> GetAsync<TValue>(string key);
        void Set<TValue>(string key, TValue value);
        Task SetAsync<TValue>(string key, TValue value);
        void Init();
        Task InitAsync();
    }

    public class SettingManager : ISettingManager, ITransientDependency
    {
        private readonly ISettingStore _settingStore;
        private readonly ICacheManager _cacheManager;

        public SettingManager(ISettingStore settingStore, ICacheManager cacheManager)
        {
            _settingStore = settingStore;
            _cacheManager = cacheManager;
        }

        public TValue Get<TValue>(string key)
        {
            Guard.Against.NullOrWhiteSpace(key, nameof(key));

            var value = _cacheManager.Get<TValue>(key);
            if (value.Equals(default(TValue)))
            {
                value = _settingStore.Get<TValue>(key);
                if (!value.Equals(default(TValue)))
                    SetInternalCache(key, value);
            }
            return value;
        }

        public async Task<TValue> GetAsync<TValue>(string key)
        {
            Guard.Against.NullOrWhiteSpace(key, nameof(key));

            var value = await _cacheManager.GetAsync<TValue>(key);
            if (value.Equals(default(TValue)))
            {
                value = await _settingStore.GetAsync<TValue>(key);
                if (!value.Equals(default(TValue)))
                    await SetInternalCacheAsync(key, value);
            }
            return value;
        }

        public void Set<TValue>(string key, TValue value)
        {
            Guard.Against.NullOrWhiteSpace(key, nameof(key));

            SetInternal(key, value);
        }

        public async Task SetAsync<TValue>(string key, TValue value)
        {
            Guard.Against.NullOrWhiteSpace(key, nameof(key));

            await SetInternalAsync(key, value);
        }

        public void Init()
        {
            var settings = _settingStore.GetAll();
            foreach (var s in settings)
            {
                var value = JsonHelper.Deserialize<object>(s.Value);
                SetInternalCache(s.Key, value);
            }
        }

        public async Task InitAsync()
        {
            var settings = await _settingStore.GetAllAsync();
            foreach (var s in settings)
            {
                var value = JsonHelper.Deserialize<object>(s.Value);
                await SetInternalCacheAsync(s.Key, value);
            }
        }

        #region Helpers

        private void SetInternalCache<TValue>(string key, TValue value)
        {
            _cacheManager.Set(key, value, new DistributedCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromDays(10000),
            });
        }

        private async Task SetInternalCacheAsync<TValue>(string key, TValue value)
        {
            await _cacheManager.SetAsync(key, value, new DistributedCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromDays(10000)
            });
        }

        private void SetInternal<TValue>(string key, TValue value)
        {
            _settingStore.Set(key, value);
            SetInternalCache(key, value);
        }

        private async Task SetInternalAsync<TValue>(string key, TValue value)
        {
            await _settingStore.SetAsync(key, value);
            await SetInternalCacheAsync(key, value);
        }

        #endregion
    }
}
