using Ardalis.GuardClauses;
using EasyForNet.Application.Dependencies;
using EasyForNet.Helpers;
using EasyForNet.Key;
using Microsoft.Extensions.Caching.Distributed;
using System.Threading;
using System.Threading.Tasks;

namespace EasyForNet.Cache;

public interface ICacheManager
{
    TValue Get<TValue>(string key);
    Task<TValue> GetAsync<TValue>(string key, CancellationToken token = default(CancellationToken));
    void Set<TValue>(string key, TValue value, DistributedCacheEntryOptions options);
    Task SetAsync<TValue>(string key, TValue value, DistributedCacheEntryOptions options, CancellationToken token = default(CancellationToken));
    void Refresh(string key);
    Task RefreshAsync(string key, CancellationToken token = default(CancellationToken));
    void Remove(string key);
    Task RemoveAsync(string key, CancellationToken token = default(CancellationToken));
}

public class CacheManager : ICacheManager, ITransientDependency
{
    private readonly IDistributedCache _distributedCache;
    private readonly IKeyManager _keyManager;

    public CacheManager(IDistributedCache distributedCache, IKeyManager keyManager)
    {
        _distributedCache = distributedCache;
        _keyManager = keyManager;
    }

    public virtual TValue Get<TValue>(string key)
    {
        Guard.Against.NullOrWhiteSpace(key, nameof(key));

        var bytes = _distributedCache.Get(_keyManager.GlobalKey(key));
        var obj = JsonHelper.Deserialize<TValue>(bytes);
        return obj;
    }

    public virtual async Task<TValue> GetAsync<TValue>(string key, CancellationToken token = default)
    {
        Guard.Against.NullOrWhiteSpace(key, nameof(key));

        var bytes = await _distributedCache.GetAsync(_keyManager.GlobalKey(key), token);
        var obj = JsonHelper.Deserialize<TValue>(bytes);
        return obj;
    }

    public virtual void Set<TValue>(string key, TValue value, DistributedCacheEntryOptions options)
    {
        Guard.Against.NullOrWhiteSpace(key, nameof(key));
        Guard.Against.Null(value, nameof(value));

        var bytes = JsonHelper.ToBytes(value);
        _distributedCache.Set(_keyManager.GlobalKey(key), bytes, options);
    }

    public virtual async Task SetAsync<TValue>(string key, TValue value, DistributedCacheEntryOptions options, CancellationToken token = default)
    {
        Guard.Against.NullOrWhiteSpace(key, nameof(key));
        Guard.Against.Null(value, nameof(value));

        var bytes = JsonHelper.ToBytes(value);
        await _distributedCache.SetAsync(_keyManager.GlobalKey(key), bytes, options, token);
    }

    public virtual void Refresh(string key)
    {
        Guard.Against.NullOrWhiteSpace(key, nameof(key));

        _distributedCache.Refresh(_keyManager.GlobalKey(key));
    }

    public virtual async Task RefreshAsync(string key, CancellationToken token = default)
    {
        Guard.Against.NullOrWhiteSpace(key, nameof(key));

        await _distributedCache.RefreshAsync(_keyManager.GlobalKey(key), token);
    }

    public virtual void Remove(string key)
    {
        Guard.Against.NullOrWhiteSpace(key, nameof(key));

        _distributedCache.Remove(_keyManager.GlobalKey(key));
    }

    public virtual async Task RemoveAsync(string key, CancellationToken token = default)
    {
        Guard.Against.NullOrWhiteSpace(key, nameof(key));

        await _distributedCache.RemoveAsync(_keyManager.GlobalKey(key), token);
    }
}