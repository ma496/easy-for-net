using EasyForNet.Cache;
using EasyForNet.Tests.Base;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace EasyForNet.Tests.Cache
{
    public class CacheManagerTests : TestsBase
    {
        public CacheManagerTests(ITestOutputHelper outputHelper) : base(outputHelper)
        {
        }

        [Fact]
        public void StringSetGetAndRemoveTest()
        {
            var key = $"name-{IncrementalId.Id}";
            var name = "Elon Musk";
            var cacheManager = Services.GetRequiredService<ICacheManager>();

            cacheManager.Set(key, name, new DistributedCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromMinutes(100)
            });

            var cacheName = cacheManager.Get(key);

            cacheName.Should().Be(name);

            cacheManager.Remove(key);

            cacheName = cacheManager.Get(key);

            cacheName.Should().BeNullOrEmpty();
        }

        [Fact]
        public async Task StringSetGetAndRemoveAsyncTest()
        {
            var key = $"name-{IncrementalId.Id}";
            var name = "Elon Musk";
            var cacheManager = Services.GetRequiredService<ICacheManager>();

            await cacheManager.SetAsync(key, name, new DistributedCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromMinutes(100)
            });

            var cacheName = await cacheManager.GetAsync(key);

            cacheName.Should().Be(name);

            await cacheManager.RemoveAsync(key);

            cacheName = await cacheManager.GetAsync(key);

            cacheName.Should().BeNullOrEmpty();
        }

        [Fact]
        public void ObjSetGetAndRemoveTest()
        {
            var key = $"car-{IncrementalId.Id}";
            var obj = new CarCacheModel
            {
                IsNew = false,
                Modal = "civic-123",
                Price = 6000000.50M,
                RunKilometer = 2000.96f,
                Year = 2022
            };
            var cacheManager = Services.GetRequiredService<ICacheManager>();

            cacheManager.Set(key, obj, new DistributedCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromMinutes(100)
            });

            var cacheObj = cacheManager.Get<CarCacheModel>(key);

            cacheObj.Should().Be(cacheObj);

            cacheManager.Remove(key);

            cacheObj = cacheManager.Get<CarCacheModel>(key);

            cacheObj.Should().BeNull();
        }

        [Fact]
        public async Task ObjSetGetAndRemoveAsyncTest()
        {
            var key = $"car-{IncrementalId.Id}";
            var obj = new CarCacheModel
            {
                IsNew = false,
                Modal = "civic-123",
                Price = 6000000.50M,
                RunKilometer = 2000.96f,
                Year = 2022
            };
            var cacheManager = Services.GetRequiredService<ICacheManager>();

            await cacheManager.SetAsync(key, obj, new DistributedCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromMinutes(100)
            });

            var cacheObj = await cacheManager.GetAsync<CarCacheModel>(key);

            cacheObj.Should().Be(cacheObj);

            await cacheManager.RemoveAsync(key);

            cacheObj = await cacheManager.GetAsync<CarCacheModel>(key);

            cacheObj.Should().BeNull();
        }

        [Fact]
        public void ExpireTest()
        {
            var key = $"name-{IncrementalId.Id}";
            var name = "Elon Musk";
            var cacheManager = Services.GetRequiredService<ICacheManager>();

            cacheManager.Set(key, name, new DistributedCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromMilliseconds(300)
            });

            Thread.Sleep(400);

            var cacheName = cacheManager.Get(key);

            cacheName.Should().BeNullOrEmpty();
        }

        [Fact]
        public void RefreshTest()
        {
            var key = $"name-{IncrementalId.Id}";
            var name = "Elon Musk";
            var cacheManager = Services.GetRequiredService<ICacheManager>();

            cacheManager.Set(key, name, new DistributedCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromMilliseconds(300)
            });

            Thread.Sleep(100);

            cacheManager.Refresh(key);

            Thread.Sleep(350);

            var cacheName = cacheManager.Get(key);

            cacheName.Should().BeNullOrEmpty();
        }

        [Fact]
        public async Task RefreshAsyncTest()
        {
            var key = $"name-{IncrementalId.Id}";
            var name = "Elon Musk";
            var cacheManager = Services.GetRequiredService<ICacheManager>();

            await cacheManager.SetAsync(key, name, new DistributedCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromMilliseconds(300)
            });

            Thread.Sleep(100);

            await cacheManager.RefreshAsync(key);

            Thread.Sleep(350);

            var cacheName = await cacheManager.GetAsync(key);

            cacheName.Should().BeNullOrEmpty();
        }

        #region Classes

        [Serializable]
        public class CarCacheModel
        {
            public string Modal { get; set; }
            public int Year { get; set; }
            public bool IsNew { get; set; }
            public float RunKilometer { get; set; }
            public decimal Price { get; set; }
        }

        #endregion
    }
}
