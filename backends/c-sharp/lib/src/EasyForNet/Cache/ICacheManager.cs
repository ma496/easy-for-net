using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;

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