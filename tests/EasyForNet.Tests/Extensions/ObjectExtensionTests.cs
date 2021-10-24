using System.Diagnostics.CodeAnalysis;
using EasyForNet.Extensions;
using EasyForNet.Tests.Base;
using Xunit;
using Xunit.Abstractions;

namespace EasyForNet.Tests.Extensions
{
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Local")]
    public class ObjectExtensionTests : TestsBase
    {
        public ObjectExtensionTests(ITestOutputHelper outputHelper) : base(outputHelper)
        {
        }

        [Fact]
        public void ToJsonTest()
        {
            var customer = new Customer();

            var customerJson = customer.ToJson();

            Assert.Contains("\"name\":null", customerJson);

            customer = new Customer {Name = "Muhammad Ali"};

            customerJson = customer.ToJson();

            Assert.Contains("\"name\":\"Muhammad Ali\"", customerJson);
        }

        private class Customer
        {
            public string Name { get; set; }
        }
    }
}