using EasyForNet.EntityFramework.Tests.Base;
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
    public class SettingStoreTests : TestsBase
    {
        private readonly ISettingStore _settingStore;

        public SettingStoreTests(ITestOutputHelper outputHelper) : base(outputHelper)
        {
            _settingStore = Services.GetRequiredService<ISettingStore>();
        }

        [Fact]
        public void CreateAndGetTest()
        {
            var key = $"key-{IncrementalId.Id}";

            _settingStore.Set(key, 123);

            var value = _settingStore.Get<int>(key);

            value.Should().Be(123);
        }

        [Fact]
        public async Task CreateAndGetAsyncTest()
        {
            var key = $"key-{IncrementalId.Id}";

            await _settingStore.SetAsync(key, 432.29);

            var value = await _settingStore.GetAsync<double>(key);

            value.ToString().Should().StartWith("432.29");
        }

        [Fact]
        public void CreateAndGetObjTest()
        {
            var key = $"key-{IncrementalId.Id}";

            var obj = CreateObject();
            _settingStore.Set(key, obj);

            var value = _settingStore.Get<CarModel>(key);

            value.Should().BeEquivalentTo(obj);
        }

        [Fact]
        public async Task CreateAndGetObjAsyncTest()
        {
            var key = $"key-{IncrementalId.Id}";

            var obj = CreateObject();
            await _settingStore.SetAsync(key, obj);

            var value = await _settingStore.GetAsync<CarModel>(key);

            value.Should().BeEquivalentTo(obj);
        }

        [Fact]
        public void CreateAndGetListTest()
        {
            var key = $"key-{IncrementalId.Id}";

            var objects = CreateObjects(10);
            _settingStore.Set(key, objects);

            var value = _settingStore.Get<List<CarModel>>(key);

            value.Should().BeEquivalentTo(objects);
        }

        [Fact]
        public async Task CreateAndGetListAsyncTest()
        {
            var key = $"key-{IncrementalId.Id}";

            var objects = CreateObjects(10);
            await _settingStore.SetAsync(key, objects);

            var value = await _settingStore.GetAsync<List<CarModel>>(key);

            value.Should().BeEquivalentTo(objects);
        }

        [Fact]
        public void GetAllTest()
        {
            for (var i = 0; i < 10; i++)
            {
                var key = $"key-{IncrementalId.Id}";

                var obj = CreateObject();
                _settingStore.Set(key, obj);
            }

            var all = _settingStore.GetAll();

            all.Count.Should().BeGreaterThanOrEqualTo(10);
        }

        [Fact]
        public async Task GetAllAsyncTest()
        {
            for (var i = 0; i < 10; i++)
            {
                var key = $"key-{IncrementalId.Id}";

                var obj = CreateObject();
                await _settingStore.SetAsync(key, obj);
            }

            var all = await _settingStore.GetAllAsync();

            all.Count.Should().BeGreaterThanOrEqualTo(10);
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
