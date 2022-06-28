using Ardalis.GuardClauses;
using EasyForNet.Application.Dependencies;
using Microsoft.Extensions.Caching.Distributed;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Threading.Tasks;

namespace EasyForNet.Cache
{
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

        public CacheManager(IDistributedCache distributedCache)
        {
            _distributedCache = distributedCache;
        }

        public TValue Get<TValue>(string key)
        {
            Guard.Against.NullOrWhiteSpace(key, nameof(key));

            var bytes = _distributedCache.Get(GlobalKey(key));
            var obj = GetObject<TValue>(bytes);
            return obj;
        }

        public async Task<TValue> GetAsync<TValue>(string key, CancellationToken token = default)
        {
            Guard.Against.NullOrWhiteSpace(key, nameof(key));

            var bytes = await _distributedCache.GetAsync(GlobalKey(key), token);
            var obj = GetObject<TValue>(bytes);
            return obj;
        }

        public void Set<TValue>(string key, TValue value, DistributedCacheEntryOptions options)
        {
            Guard.Against.NullOrWhiteSpace(key, nameof(key));

            var bytes = GetBytes(value);
            _distributedCache.Set(GlobalKey(key), bytes, options);
        }

        public async Task SetAsync<TValue>(string key, TValue value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            Guard.Against.NullOrWhiteSpace(key, nameof(key));

            var bytes = GetBytes(value);
            await _distributedCache.SetAsync(GlobalKey(key), bytes, options, token);
        }

        public void Refresh(string key)
        {
            Guard.Against.NullOrWhiteSpace(key, nameof(key));

            _distributedCache.Refresh(GlobalKey(key));
        }

        public async Task RefreshAsync(string key, CancellationToken token = default)
        {
            Guard.Against.NullOrWhiteSpace(key, nameof(key));

            await _distributedCache.RefreshAsync(GlobalKey(key), token);
        }

        public void Remove(string key)
        {
            Guard.Against.NullOrWhiteSpace(key, nameof(key));

            _distributedCache.Remove(GlobalKey(key));
        }

        public async Task RemoveAsync(string key, CancellationToken token = default)
        {
            Guard.Against.NullOrWhiteSpace(key, nameof(key));

            await _distributedCache.RemoveAsync(GlobalKey(key), token);
        }

        #region Helpers

        private byte[] GetBytes<T>(T obj)
        {
            var bf = new BinaryFormatter(); 
            using var ms = new MemoryStream();
            // TODO use other mehod for serialize
            bf.Serialize(ms, obj); 
            return ms.ToArray();
        }

        private T GetObject<T>(byte[] bytes)
        {
            if (bytes == null)
                return default(T);
            var bf = new BinaryFormatter();
            using var ms = new MemoryStream();
            ms.Write(bytes, 0, bytes.Length);
            ms.Seek(0, SeekOrigin.Begin);
            // TODO use other mehod for deserialize
            var obj = bf.Deserialize(ms);
            return (T)obj;
        }

        private string GlobalKey(string key)
        {
            return $"Global_{key}";
        }

        #endregion
    }
}
