using System;
using System.Linq;
using EasyForNet.Helpers;
using EasyForNet.Tests.Base;
using Xunit;
using Xunit.Abstractions;

namespace EasyForNet.Tests.Helpers
{
    public class DateHelperTests : TestsBase
    {
        public DateHelperTests(ITestOutputHelper outputHelper) : base(outputHelper)
        {
        }

        [Fact]
        public void EachDateTest()
        {
            var dates = DateHelper.EachDate(new DateTime(2020, 2, 10), new DateTime(2020, 2, 23)).ToList();

            Assert.Equal(14, dates.Count);
        }
    }
}