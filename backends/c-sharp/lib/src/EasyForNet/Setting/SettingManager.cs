using Ardalis.GuardClauses;
using EasyForNet.Application.Dependencies;
using EasyForNet.Cache;
using EasyForNet.Entities;
using Microsoft.Extensions.Caching.Distributed;
using System;
using System.Threading.Tasks;

namespace EasyForNet.Setting;

public class SettingManager : ISettingManager, ITransientDependency
{
    private const string KeyPrefix = "Settings";
    private readonly ISettingStore<EfnSetting> _settingStore;
    private readonly ICacheManager _cacheManager;

    public SettingManager(ISettingStore<EfnSetting> settingStore, ICacheManager cacheManager)
    {
        _settingStore = settingStore;
        _cacheManager = cacheManager;
    }

    public TValue Get<TValue>(string key)
    {
        Guard.Against.NullOrWhiteSpace(key, nameof(key));

        var value = _cacheManager.Get<TValue>(GetKey(key));
        if (value == null)
        {
            value = _settingStore.Get<TValue>(GetKey(key));
            if (value != null)
                SetInternalCache(GetKey(key), value);
        }
        return value;
    }

    public async Task<TValue> GetAsync<TValue>(string key)
    {
        Guard.Against.NullOrWhiteSpace(key, nameof(key));

        var value = await _cacheManager.GetAsync<TValue>(GetKey(key));
        if (value == null)
        {
            value = await _settingStore.GetAsync<TValue>(GetKey(key));
            if (value != null)
                await SetInternalCacheAsync(GetKey(key), value);
        }
        return value;
    }

    public void Set<TValue>(string key, TValue value)
    {
        Guard.Against.NullOrWhiteSpace(key, nameof(key));
        Guard.Against.Null(value, nameof(value));

        SetInternal(GetKey(key), value);
    }

    public async Task SetAsync<TValue>(string key, TValue value)
    {
        Guard.Against.NullOrWhiteSpace(key, nameof(key));
        Guard.Against.Null(value, nameof(value));

        await SetInternalAsync(GetKey(key), value);
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

    private string GetKey(string key)
    {
        return !key.StartsWith($"{KeyPrefix}_") ? $"{KeyPrefix}_{key}" : key;
    }

    #endregion
}