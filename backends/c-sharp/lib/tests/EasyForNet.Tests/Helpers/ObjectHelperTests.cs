using System;
using System.Diagnostics.CodeAnalysis;
using EasyForNet.Helpers;
using EasyForNet.Tests.Base;
using Xunit;
using Xunit.Abstractions;

namespace EasyForNet.Tests.Helpers;

[SuppressMessage("ReSharper", "UnassignedGetOnlyAutoProperty")]
public class ObjectHelperTests : TestsBase
{
    public ObjectHelperTests(ITestOutputHelper outputHelper) : base(outputHelper)
    {
    }

    [Fact]
    public void PropertyInfoTest()
    {
        var propertyInfo = ObjectHelper.PropertyInfo<Customer>(c => c.Name);

        Assert.Equal(typeof(Customer), propertyInfo.ReflectedType);
        Assert.Equal(nameof(Customer.Name), propertyInfo.Name);
    }

    [Fact]
    public void PropertyInfo_MethodTest()
    {
        Assert.Throws<ArgumentException>(() => ObjectHelper.PropertyInfo<Customer>(c => c.GetOrder()));
    }

    [Fact]
    public void PropertyInfo_FieldTest()
    {
        Assert.Throws<ArgumentException>(() => ObjectHelper.PropertyInfo<Customer>(c => c._name));
    }

    [Fact]
    public void PropertyInfo_ConstantTest()
    {
        Assert.Throws<ArgumentException>(() => ObjectHelper.PropertyInfo<Customer>(c => 13));
    }

    [Fact]
    public void PropertyInfo_ObjectTest()
    {
        Assert.Throws<ArgumentException>(() => ObjectHelper.PropertyInfo<Customer>(c => new Customer()));

        var customer = new Customer();
        Assert.Throws<ArgumentException>(() => ObjectHelper.PropertyInfo<Customer>(c => customer));
    }

    [Fact]
    public void PropertyInfo_OtherClassPropertyTest()
    {
        var argumentException =
            Assert.Throws<ArgumentException>(() =>
                ObjectHelper.PropertyInfo<Customer>(c => new CustomerOrder().OrderNo));

        Assert.Contains("refers to a property that is not from type", argumentException.Message);

        var order = new CustomerOrder();
        argumentException =
            Assert.Throws<ArgumentException>(() => ObjectHelper.PropertyInfo<Customer>(c => order.OrderNo));

        Assert.Contains("refers to a property that is not from type", argumentException.Message);
    }

    private class Customer
    {
        public readonly string _name = "";

        public string Name => _name;

        public object GetOrder()
        {
            return null;
        }
    }

    private class CustomerOrder
    {
        public long OrderNo { get; }
    }
}