using Ardalis.GuardClauses;
using EasyForNet.Application.Dependencies;
using EasyForNet.Cache;
using EasyForNet.Entities;
using Microsoft.Extensions.Caching.Distributed;
using System;
using System.Threading.Tasks;

namespace EasyForNet.Setting;

public interface ISettingManager
{
    TValue Get<TValue>(string key);
    Task<TValue> GetAsync<TValue>(string key);
    void Set<TValue>(string key, TValue value);
    Task SetAsync<TValue>(string key, TValue value);
}

public class SettingManager : ISettingManager, ITransientDependency
{
    private readonly ISettingStore<EfnSettingEntity> _settingStore;
    private readonly ICacheManager _cacheManager;

    public SettingManager(ISettingStore<EfnSettingEntity> settingStore, ICacheManager cacheManager)
    {
        _settingStore = settingStore;
        _cacheManager = cacheManager;
    }

    public TValue Get<TValue>(string key)
    {
        Guard.Against.NullOrWhiteSpace(key, nameof(key));

        var value = _cacheManager.Get<TValue>(key);
        if (value == null)
        {
            value = _settingStore.Get<TValue>(key);
            if (value != null)
                SetInternalCache(key, value);
        }
        return value;
    }

    public async Task<TValue> GetAsync<TValue>(string key)
    {
        Guard.Against.NullOrWhiteSpace(key, nameof(key));

        var value = await _cacheManager.GetAsync<TValue>(key);
        if (value == null)
        {
            value = await _settingStore.GetAsync<TValue>(key);
            if (value != null)
                await SetInternalCacheAsync(key, value);
        }
        return value;
    }

    public void Set<TValue>(string key, TValue value)
    {
        Guard.Against.NullOrWhiteSpace(key, nameof(key));
        Guard.Against.Null(value, nameof(value));

        SetInternal(key, value);
    }

    public async Task SetAsync<TValue>(string key, TValue value)
    {
        Guard.Against.NullOrWhiteSpace(key, nameof(key));
        Guard.Against.Null(value, nameof(value));

        await SetInternalAsync(key, value);
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