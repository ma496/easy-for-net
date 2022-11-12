using EasyForNet.Application.Dependencies;
using EasyForNet.Tests.Base;
using EasyForNet.Tests.Share;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace EasyForNet.Tests.Application.Dependencies
{
    public class SingletonDependencyTests : TestsBase
    {
        public SingletonDependencyTests(ITestOutputHelper outputHelper) : base(outputHelper)
        {
        }

        [Fact]
        public void ClassTest()
        {
            var classOne = Services.GetService<ClassOne>();

            Assert.NotNull(classOne);
            Assert.Equal(1, ClassOne.InstanceCount);

            using var scope = GlobalObjects.ServiceProvider.CreateScope();
            scope.ServiceProvider.GetService<ClassOne>();

            Assert.Equal(1, ClassOne.InstanceCount);

            var classOneInterface = Services.GetService<IClassOne>();

            Assert.NotNull(classOneInterface);
        }

        private interface IClassOne
        {
        }

        private class ClassOne : IClassOne, ISingletonDependency
        {
            public ClassOne()
            {
                InstanceCount++;
            }

            public static int InstanceCount { get; private set; }
        }
    }
}