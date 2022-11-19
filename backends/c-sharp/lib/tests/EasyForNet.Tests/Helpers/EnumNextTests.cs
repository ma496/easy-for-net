using System;
using System.Threading.Tasks;
using EasyForNet.Helpers;
using EasyForNet.Tests.Base;
using Xunit;
using Xunit.Abstractions;

namespace EasyForNet.Tests.Helpers
{
    public class EnumNextTests : TestsBase
    {
        public EnumNextTests(ITestOutputHelper outputHelper) : base(outputHelper)
        {
        }

        [Fact]
        public void Next()
        {
            var enumNext = new EnumNext<DayOfWeek>();

            Assert.Equal(DayOfWeek.Sunday, enumNext.Next());
            Assert.Equal(DayOfWeek.Monday, enumNext.Next());
            Assert.Equal(DayOfWeek.Tuesday, enumNext.Next());
            Assert.Equal(DayOfWeek.Wednesday, enumNext.Next());
            Assert.Equal(DayOfWeek.Thursday, enumNext.Next());
            Assert.Equal(DayOfWeek.Friday, enumNext.Next());
            Assert.Equal(DayOfWeek.Saturday, enumNext.Next());
            Assert.Equal(DayOfWeek.Sunday, enumNext.Next());
            Assert.Equal(DayOfWeek.Monday, enumNext.Next());
        }

        [Fact]
        public void Next_Reset()
        {
            var enumNext = new EnumNext<DayOfWeek>();

            Assert.Equal(DayOfWeek.Sunday, enumNext.Next());
            Assert.Equal(DayOfWeek.Monday, enumNext.Next());
            Assert.Equal(DayOfWeek.Tuesday, enumNext.Next());
            Assert.Equal(DayOfWeek.Wednesday, enumNext.Next());
            Assert.Equal(DayOfWeek.Thursday, enumNext.Next());
            Assert.Equal(DayOfWeek.Friday, enumNext.Next());
            Assert.Equal(DayOfWeek.Saturday, enumNext.Next());
            Assert.Equal(DayOfWeek.Sunday, enumNext.Next());
            Assert.Equal(DayOfWeek.Monday, enumNext.Next());

            enumNext.Reset();

            Assert.Equal(DayOfWeek.Sunday, enumNext.Next());
            Assert.Equal(DayOfWeek.Monday, enumNext.Next());
            Assert.Equal(DayOfWeek.Tuesday, enumNext.Next());
        }

        [Fact]
        public async Task Next_Parallel()
        {
            var action = new Action(() =>
            {
                OutputHelper.WriteLine("Start");

                var enumNext = new EnumNext<DayOfWeek>();

                Assert.Equal(DayOfWeek.Sunday, enumNext.Next());
                Assert.Equal(DayOfWeek.Monday, enumNext.Next());
                Assert.Equal(DayOfWeek.Tuesday, enumNext.Next());
                Assert.Equal(DayOfWeek.Wednesday, enumNext.Next());
                Assert.Equal(DayOfWeek.Thursday, enumNext.Next());
                Assert.Equal(DayOfWeek.Friday, enumNext.Next());
                Assert.Equal(DayOfWeek.Saturday, enumNext.Next());
                Assert.Equal(DayOfWeek.Sunday, enumNext.Next());
                Assert.Equal(DayOfWeek.Monday, enumNext.Next());

                OutputHelper.WriteLine("End");
            });

            var tasks = new[]
            {
                Task.Run(action),
                Task.Run(action),
                Task.Run(action),
                Task.Run(action),
                Task.Run(action)
            };

            await Task.WhenAll(tasks);
        }

        [Fact]
        public async Task Next_Reset_Parallel()
        {
            var action = new Action(() =>
            {
                OutputHelper.WriteLine("Start");

                var enumNext = new EnumNext<DayOfWeek>();

                Assert.Equal(DayOfWeek.Sunday, enumNext.Next());
                Assert.Equal(DayOfWeek.Monday, enumNext.Next());
                Assert.Equal(DayOfWeek.Tuesday, enumNext.Next());
                Assert.Equal(DayOfWeek.Wednesday, enumNext.Next());
                Assert.Equal(DayOfWeek.Thursday, enumNext.Next());
                Assert.Equal(DayOfWeek.Friday, enumNext.Next());
                Assert.Equal(DayOfWeek.Saturday, enumNext.Next());
                Assert.Equal(DayOfWeek.Sunday, enumNext.Next());
                Assert.Equal(DayOfWeek.Monday, enumNext.Next());

                enumNext.Reset();

                Assert.Equal(DayOfWeek.Sunday, enumNext.Next());
                Assert.Equal(DayOfWeek.Monday, enumNext.Next());
                Assert.Equal(DayOfWeek.Tuesday, enumNext.Next());

                OutputHelper.WriteLine("End");
            });

            var tasks = new[]
            {
                Task.Run(action),
                Task.Run(action),
                Task.Run(action),
                Task.Run(action),
                Task.Run(action)
            };

            await Task.WhenAll(tasks);
        }
    }
}