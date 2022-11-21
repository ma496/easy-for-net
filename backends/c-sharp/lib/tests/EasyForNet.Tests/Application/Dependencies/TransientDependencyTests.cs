using Autofac;
using EasyForNet.Application.Dependencies;
using EasyForNet.Tests.Base;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace EasyForNet.Tests.Application.Dependencies;

public class TransientDependencyTests : TestsBase
{
    public TransientDependencyTests(ITestOutputHelper outputHelper) : base(outputHelper)
    {
    }

    [Fact]
    public void ClassTest()
    {
        var classOne = Scope.Resolve<ClassOne>();

        Assert.NotNull(classOne);
        Assert.Equal(1, ClassOne.InstanceCount);

        Scope.Resolve<ClassOne>();
        Assert.Equal(2, ClassOne.InstanceCount);

        var classOneInterface = Scope.Resolve<IClassOne>();

        Assert.NotNull(classOneInterface);
    }

    private interface IClassOne
    {
    }

    private class ClassOne : IClassOne, ITransientDependency
    {
        public ClassOne()
        {
            InstanceCount++;
        }

        public static int InstanceCount { get; private set; }
    }
}