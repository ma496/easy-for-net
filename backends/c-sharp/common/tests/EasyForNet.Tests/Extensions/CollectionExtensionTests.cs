using System.Collections.Generic;
using EasyForNet.Extensions;
using EasyForNet.Tests.Base;
using Xunit;
using Xunit.Abstractions;

namespace EasyForNet.Tests.Extensions
{
    public class CollectionExtensionTests : TestsBase
    {
        public CollectionExtensionTests(ITestOutputHelper outputHelper) : base(outputHelper)
        {
        }

        [Fact]
        public void IsNullOrEmptyTest()
        {
            List<int> numbers = null;

            Assert.True(numbers.IsNullOrEmpty());

            numbers = new List<int>();

            Assert.True(numbers.IsNullOrEmpty());

            numbers.Add(12);

            Assert.False(numbers.IsNullOrEmpty());
        }
    }
}