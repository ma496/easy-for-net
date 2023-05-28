using System.Threading;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using EasyForNet.Application.Dependencies;
using EasyForNet.Helpers;
using Microsoft.Extensions.Caching.Distributed;

namespace EasyForNet.Cache;

public class CacheManager : ICacheManager, ITransientDependency
{
    private readonly IDistributedCache _distributedCache;

    public CacheManager(IDistributedCache distributedCache)
    {
        _distributedCache = distributedCache;
    }

    public virtual TValue Get<TValue>(string key)
    {
        Guard.Against.NullOrWhiteSpace(key, nameof(key));

        var bytes = _distributedCache.Get(key);
        var obj = JsonHelper.Deserialize<TValue>(bytes);
        return obj;
    }

    public virtual async Task<TValue> GetAsync<TValue>(string key, CancellationToken token = default)
    {
        Guard.Against.NullOrWhiteSpace(key, nameof(key));

        var bytes = await _distributedCache.GetAsync(key, token);
        var obj = JsonHelper.Deserialize<TValue>(bytes);
        return obj;
    }

    public virtual void Set<TValue>(string key, TValue value, DistributedCacheEntryOptions options)
    {
        Guard.Against.NullOrWhiteSpace(key, nameof(key));
        Guard.Against.Null(value, nameof(value));

        var bytes = JsonHelper.ToBytes(value);
        _distributedCache.Set(key, bytes, options);
    }

    public virtual async Task SetAsync<TValue>(string key, TValue value, DistributedCacheEntryOptions options, CancellationToken token = default)
    {
        Guard.Against.NullOrWhiteSpace(key, nameof(key));
        Guard.Against.Null(value, nameof(value));

        var bytes = JsonHelper.ToBytes(value);
        await _distributedCache.SetAsync(key, bytes, options, token);
    }

    public virtual void Refresh(string key)
    {
        Guard.Against.NullOrWhiteSpace(key, nameof(key));

        _distributedCache.Refresh(key);
    }

    public virtual async Task RefreshAsync(string key, CancellationToken token = default)
    {
        Guard.Against.NullOrWhiteSpace(key, nameof(key));

        await _distributedCache.RefreshAsync(key, token);
    }

    public virtual void Remove(string key)
    {
        Guard.Against.NullOrWhiteSpace(key, nameof(key));

        _distributedCache.Remove(key);
    }

    public virtual async Task RemoveAsync(string key, CancellationToken token = default)
    {
        Guard.Against.NullOrWhiteSpace(key, nameof(key));

        await _distributedCache.RemoveAsync(key, token);
    }
}