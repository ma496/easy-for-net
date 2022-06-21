using Ardalis.GuardClauses;
using EasyForNet.Application.Dependencies;
using Microsoft.Extensions.Caching.Distributed;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
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

    public class CacheManager : ICacheManager, ISingletonDependency
    {
        private readonly IDistributedCache _distributedCache;

        public CacheManager(IDistributedCache distributedCache)
        {
            _distributedCache = distributedCache;
        }

        public TValue Get<TValue>(string key)
        {
            Guard.Against.NullOrWhiteSpace(key);

            var bytes = _distributedCache.Get(GetKey(key));
            var obj = GetObject<TValue>(bytes);
            return obj;
        }

        public async Task<TValue> GetAsync<TValue>(string key, CancellationToken token = default)
        {
            var bytes = await _distributedCache.GetAsync(GetKey(key), token);
            var obj = GetObject<TValue>(bytes);
            return obj;
        }

        public void Set<TValue>(string key, TValue value, DistributedCacheEntryOptions options)
        {
            var bytes = GetBytes(value);
            _distributedCache.Set(GetKey(key), bytes, options);
        }

        public async Task SetAsync<TValue>(string key, TValue value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            var bytes = GetBytes(value);
            await _distributedCache.SetAsync(GetKey(key), bytes, options, token);
        }

        public void Refresh(string key)
        {
            _distributedCache.Refresh(GetKey(key));
        }

        public async Task RefreshAsync(string key, CancellationToken token = default)
        {
            await _distributedCache.RefreshAsync(GetKey(key), token);
        }

        public void Remove(string key)
        {
            _distributedCache.Remove(GetKey(key));
        }

        public async Task RemoveAsync(string key, CancellationToken token = default)
        {
            await _distributedCache.RemoveAsync(GetKey(key), token);
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

        private string GetKey(string key)
        {
            return $"globall-{key}";
        }

        #endregion
    }
}
