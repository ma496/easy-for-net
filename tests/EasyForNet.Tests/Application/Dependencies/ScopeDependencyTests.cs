using EasyForNet.Application.Dependencies;
using EasyForNet.Tests.Base;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace EasyForNet.Tests.Application.Dependencies
{
    public class ScopeDependencyTests : TestsBase
    {
        public ScopeDependencyTests(ITestOutputHelper outputHelper) : base(outputHelper)
        {
        }

        [Fact]
        public void ClassTest()
        {
            var classOne = Services.GetService<ClassOne>();

            Assert.NotNull(classOne);
            Assert.Equal(1, ClassOne.InstanceCount);

            Services.GetService<ClassOne>();
            Assert.Equal(1, ClassOne.InstanceCount);

            var classOneInterface = Services.GetService<IClassOne>();

            Assert.NotNull(classOneInterface);
        }

        private interface IClassOne
        {
        }

        private class ClassOne : IClassOne, IScopedDependency
        {
            public ClassOne()
            {
                InstanceCount++;
            }

            public static int InstanceCount { get; private set; }
        }
    }
}