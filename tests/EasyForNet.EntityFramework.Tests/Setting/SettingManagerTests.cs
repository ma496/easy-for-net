using EasyForNet.Cache;
using EasyForNet.EntityFramework.Tests.Base;
using EasyForNet.Key;
using EasyForNet.Setting;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace EasyForNet.EntityFramework.Tests.Setting
{
    public class SettingManagerTests : TestsBase
    {
        private readonly ISettingManager _settingManager;
        private readonly ISettingStore _settingStore;
        private readonly ICacheManager _cacheManager;
        private readonly IKeyManager _keyManager;

        public SettingManagerTests(ITestOutputHelper outputHelper) : base(outputHelper)
        {
            _settingManager = Services.GetRequiredService<ISettingManager>();
            _settingStore = Services.GetRequiredService<ISettingStore>();
            _cacheManager = Services.GetRequiredService<ICacheManager>();
            _keyManager = Services.GetRequiredService<IKeyManager>();
        }

        [Fact]
        public void CreateAndGetTest()
        {
            var key = $"key-{IncrementalId.Id}";

            _settingManager.Set(key, 123);

            var value = _settingManager.Get<int>(key);

            value.Should().Be(123);
        }

        [Fact]
        public async Task CreateAndGetAsyncTest()
        {
            var key = $"key-{IncrementalId.Id}";

            await _settingManager.SetAsync(key, 432.29);

            var value = await _settingManager.GetAsync<double>(key);

            value.ToString().Should().StartWith("432.29");
        }

        [Fact]
        public void CreateAndGetObjTest()
        {
            var key = $"key-{IncrementalId.Id}";

            var obj = CreateObject();
            _settingManager.Set(key, obj);

            var value = _settingManager.Get<CarModel>(key);

            value.Should().BeEquivalentTo(obj);
        }

        [Fact]
        public async Task CreateAndGetObjAsyncTest()
        {
            var key = $"key-{IncrementalId.Id}";

            var obj = CreateObject();
            await _settingManager.SetAsync(key, obj);

            var value = await _settingManager.GetAsync<CarModel>(key);

            value.Should().BeEquivalentTo(obj);
        }

        [Fact]
        public void CreateAndGetListTest()
        {
            var key = $"key-{IncrementalId.Id}";

            var objects = CreateObjects(10);
            _settingManager.Set(key, objects);

            var value = _settingManager.Get<List<CarModel>>(key);

            value.Should().BeEquivalentTo(objects);
        }

        [Fact]
        public async Task CreateAndGetListAsyncTest()
        {
            var key = $"key-{IncrementalId.Id}";

            var objects = CreateObjects(10);
            await _settingManager.SetAsync(key, objects);

            var value = await _settingManager.GetAsync<List<CarModel>>(key);

            value.Should().BeEquivalentTo(objects);
        }

        [Fact]
        public void InitTest()
        {
            var oldSettingCount = _settingStore.GetAll().Count;

            var objects = new Dictionary<string, CarModel>();
            for (var i = 0; i < 10; i++)
            {
                var key = $"key-{IncrementalId.Id}";

                var obj = CreateObject();
                _settingStore.Set(key, obj);
                objects.Add(key, obj);
            }

            _settingManager.Init();

            var allSettingCount = _settingStore.GetAll().Count;

            (allSettingCount - oldSettingCount).Should().BeGreaterThanOrEqualTo(10);

            foreach (var o in objects)
            {
                var cacheObj = _cacheManager.Get<CarModel>(o.Key);

                cacheObj.Should().BeEquivalentTo(o.Value);
            }
        }

        [Fact]
        public async Task InitAsyncTest()
        {
            var oldSettingCount = (await _settingStore.GetAllAsync()).Count;

            var objects = new Dictionary<string, CarModel>();
            for (var i = 0; i < 10; i++)
            {
                var key = $"key-{IncrementalId.Id}";

                var obj = CreateObject();
                await _settingStore.SetAsync(key, obj);
                objects.Add(key, obj);
            }

            await _settingManager.InitAsync();

            var allSettingCount = (await _settingStore.GetAllAsync()).Count;

            (allSettingCount - oldSettingCount).Should().BeGreaterThanOrEqualTo(10);

            foreach (var o in objects)
            {
                var cacheObj = _cacheManager.Get<CarModel>(o.Key);

                cacheObj.Should().BeEquivalentTo(o.Value);
            }
        }

        #region Methods

        private CarModel CreateObject()
        {
            return new CarModel
            {
                Id = 123 + IncrementalId.Id,
                Model = $"Civic-2022_{IncrementalId.Id}",
                IssueDate = DateTime.Now.AddDays(IncrementalId.Id),
                Price = 6000000 + IncrementalId.Id,
                RunInKm = 300.538F + IncrementalId.Id,
                IsNew = new Random().Next(100) <= 50 ? true : false
            };
        }

        private List<CarModel> CreateObjects(int count)
        {
            var objects = new List<CarModel>();
            for (var i = 0; i < count; i++)
            {
                objects.Add(CreateObject());
            }
            return objects;
        }

        #endregion

        #region Classes

        public class CarModel
        {
            public long Id { get; set; }
            public string Model { get; set; }
            public DateTime IssueDate { get; set; }
            public decimal Price { get; set; }
            public float RunInKm { get; set; }
            public bool IsNew { get; set; }
        }

        #endregion
    }
}
