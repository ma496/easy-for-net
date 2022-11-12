using EasyForNet.Helpers;
using EasyForNet.Tests.Base;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace EasyForNet.Tests.Helpers
{
    public class JsonHelperTests : TestsBase
    {
        public JsonHelperTests(ITestOutputHelper outputHelper) : base(outputHelper)
        {
        }

        [Fact]
        public void IntToJsonToIntTest()
        {
            var number = 700;

            var json = JsonHelper.ToJson(number);
            var deserializedNumber = JsonHelper.Deserialize<int>(json);

            deserializedNumber.Should().Be(number);
        }

        [Fact]
        public void IntToBytesToIntTest()
        {
            var number = 700;

            var bytes = JsonHelper.ToBytes(number);
            var deserializedNumber = JsonHelper.Deserialize<int>(bytes);

            deserializedNumber.Should().Be(number);
        }

        [Fact]
        public void LongToJsonToLongTest()
        {
            var number = 700L;

            var json = JsonHelper.ToJson(number);
            var deserializedNumber = JsonHelper.Deserialize<long>(json);

            deserializedNumber.Should().Be(number);
        }

        [Fact]
        public void LongToBytesToLongTest()
        {
            var number = 700L;

            var bytes = JsonHelper.ToBytes(number);
            var deserializedNumber = JsonHelper.Deserialize<long>(bytes);

            deserializedNumber.Should().Be(number);
        }

        [Fact]
        public void FloatToJsonToFloatTest()
        {
            var number = 983.554f;

            var json = JsonHelper.ToJson(number);
            var deserializedNumber = JsonHelper.Deserialize<float>(json);

            deserializedNumber.Should().Be(number);
        }

        [Fact]
        public void FloatToBytesToFloatTest()
        {
            var number = 983.554f;

            var bytes = JsonHelper.ToBytes(number);
            var deserializedNumber = JsonHelper.Deserialize<float>(bytes);

            deserializedNumber.Should().Be(number);
        }

        [Fact]
        public void DoubleToJsonToDoubleTest()
        {
            var number = 983.554;

            var json = JsonHelper.ToJson(number);
            var deserializedNumber = JsonHelper.Deserialize<double>(json);

            deserializedNumber.Should().Be(number);
        }

        [Fact]
        public void DoubleToBytesToDoubleTest()
        {
            var number = 983.554;

            var bytes = JsonHelper.ToBytes(number);
            var deserializedNumber = JsonHelper.Deserialize<double>(bytes);

            deserializedNumber.Should().Be(number);
        }

        [Fact]
        public void DecimalToJsonToDecimalTest()
        {
            var number = 983.554M;

            var json = JsonHelper.ToJson(number);
            var deserializedNumber = JsonHelper.Deserialize<decimal>(json);

            deserializedNumber.Should().Be(number);
        }

        [Fact]
        public void DecimalToBytesToDecimalTest()
        {
            var number = 983.554M;

            var bytes = JsonHelper.ToBytes(number);
            var deserializedNumber = JsonHelper.Deserialize<decimal>(bytes);

            deserializedNumber.Should().Be(number);
        }

        [Fact]
        public void StringToJsonToStringTest()
        {
            var name = "Muhammad Ali";

            var json = JsonHelper.ToJson(name);
            var deserializedName = JsonHelper.Deserialize<string>(json);

            deserializedName.Should().Be(name);
        }

        [Fact]
        public void StringToBytesToStringTest()
        {
            var name = "Muhammad Ali";

            var bytes = JsonHelper.ToBytes(name);
            var deserializedName = JsonHelper.Deserialize<string>(bytes);

            deserializedName.Should().Be(name);
        }

        [Fact]
        public void ObjToJsonToObjTest()
        {
            var obj = CreateObject();

            var json = JsonHelper.ToJson(obj);
            var deserializeObj = JsonHelper.Deserialize<CarModel>(json);

            deserializeObj.Should().BeEquivalentTo(obj);
        }

        [Fact]
        public void ObjToBytesToObjTest()
        {
            var obj = CreateObject();

            var bytes = JsonHelper.ToBytes(obj);
            var deserializeObj = JsonHelper.Deserialize<CarModel>(bytes);

            deserializeObj.Should().BeEquivalentTo(obj);
        }

        [Fact]
        public void ObjectsToJsonToObjectsTest()
        {
            var objects = CreateObjects(10);

            var json = JsonHelper.ToJson(objects);
            var deserializeObjects = JsonHelper.Deserialize<List<CarModel>>(json);

            deserializeObjects.Should().BeEquivalentTo(objects);
        }

        [Fact]
        public void ObjectsToBytesToObjectsTest()
        {
            var objects = CreateObjects(10);

            var bytes = JsonHelper.ToBytes(objects);
            var deserializeObjects = JsonHelper.Deserialize<List<CarModel>>(bytes);

            deserializeObjects.Should().BeEquivalentTo(objects);
        }

        [Fact]
        public void NullToJsonTest()
        {
            Assert.Throws<ArgumentNullException>(() => JsonHelper.ToJson<object>(null));
        }

        [Fact]
        public void NullToBytesTest()
        {
            Assert.Throws<ArgumentNullException>(() => JsonHelper.ToBytes<object>(null));
        }

        [Fact]
        public void JsonStringToBytesToNumberTest()
        {
            var json = "123";

            var bytes = JsonHelper.ToBytes(json);
            var exception = Assert.Throws<JsonException>(() => JsonHelper.Deserialize<int>(bytes));

            exception.Message.Should().Contain("The JSON value could not be converted to System.Int32. Path: $ | LineNumber: 0 | BytePositionInLine: 5.");
        }

        [Fact]
        public void JsonStringToBytesToObjectTest()
        {
            var json = "{\"id\":124,\"model\":\"Civic - 2022_2\",\"issueDate\":\"2022 - 07 - 24T16: 36:50.4704322 + 05:00\",\"price\":6000004,\"runInKm\":305.538,\"isNew\":false}";

            var bytes = JsonHelper.ToBytes(json);
            var exception = Assert.Throws<JsonException>(() => JsonHelper.Deserialize<CarModel>(bytes));

            exception.Message.Should().Contain("The JSON value could not be converted to EasyForNet.Tests.Helpers.JsonHelperTests+CarModel. Path: $ | LineNumber: 0 | BytePositionInLine: 225.");
        }

        #region Helpers

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

        private class CarModel
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
