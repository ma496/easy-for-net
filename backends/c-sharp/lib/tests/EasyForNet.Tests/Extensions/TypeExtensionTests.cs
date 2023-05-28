using System.Diagnostics.CodeAnalysis;
using EasyForNet.Extensions;
using EasyForNet.Tests.Base;
using Xunit;
using Xunit.Abstractions;

namespace EasyForNet.Tests.Extensions;

public class TypeExtensionTests : TestsBase
{
    public TypeExtensionTests(ITestOutputHelper outputHelper) : base(outputHelper)
    {
    }

    [Fact]
    public void IsPrimitiveOrString()
    {
        var strType = typeof(string);
        var intType = typeof(int);

        Assert.True(strType.IsPrimitiveOrString());
        Assert.True(intType.IsPrimitiveOrString());
    }

    [Fact]
    public void IsConcreteTest()
    {
        var customerType = typeof(Customer);

        Assert.True(customerType.IsConcreteClass());

        var customerBaseType = typeof(CustomerBase);

        Assert.False(customerBaseType.IsConcreteClass());
    }

    [Fact]
    public void GetConstantTest()
    {
        var constants = typeof(LuckyNumbers).GetConstants();

        Assert.Equal(3, constants.Count);
    }

    private class Customer
    {
    }

    private abstract class CustomerBase
    {
    }

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private class LuckyNumbers
    {
        public const int One = 34;

        public const int Two = 32;

        public const int Three = 5;
    }
}