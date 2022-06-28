using Ardalis.GuardClauses;
using EasyForNet.Application.Dependencies;
using EasyForNet.Cache;
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

            return _settingStore.Get<TValue>(GlobalKey(key));
        }

        public async Task<TValue> GetAsync<TValue>(string key)
        {
            Guard.Against.NullOrWhiteSpace(key, nameof(key));

            return await _settingStore.GetAsync<TValue>(GlobalKey(key));
        }

        public void Set<TValue>(string key, TValue value)
        {
            Guard.Against.NullOrWhiteSpace(key, nameof(key));

            _settingStore.Set(GlobalKey(key), value);
        }

        public async Task SetAsync<TValue>(string key, TValue value)
        {
            Guard.Against.NullOrWhiteSpace(key, nameof(key));

            await _settingStore.SetAsync(GlobalKey(key), value);
        }

        #region Helpers

        private string GlobalKey(string key)
        {
            return $"Global_{key}";
        }

        #endregion
    }
}
