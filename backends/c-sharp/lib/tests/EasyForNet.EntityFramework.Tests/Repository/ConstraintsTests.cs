using EasyForNet.EntityFramework.Tests.Data.Entities;
using EasyForNet.EntityFramework.Tests.GenerateData;
using EasyForNet.Repository;
using EntityFramework.Exceptions.Common;
using Xunit;
using EasyForNet.EntityFramework.Tests.Base;
using Xunit.Abstractions;
using Autofac;
using System.Text.RegularExpressions;
using FluentAssertions;

namespace EasyForNet.EntityFramework.Tests.Repository;

public class ConstraintsTests : TestsBase
{
    public ConstraintsTests(ITestOutputHelper outputHelper) : base(outputHelper)
    {
    }

    [Fact]
    public void Should_Throw_UniqueConstraintException()
    {
        var repository = Scope.Resolve<IRepository<CustomerEntity, long>>();
        var generator = Scope.Resolve<CustomerGenerator>();
        var entityOne = generator.Generate();
        var entityTwo = generator.Generate();
        entityTwo.Code = entityOne.Code;

        var exception = Assert.Throws<UniqueConstraintException>(() =>
        {
            repository.Create(entityOne, true);
            repository.Create(entityTwo, true);
        });

        var exceptionMsg = Regex.Match(exception.InnerException.Message, "(?<=[']).+(?=['])").Value;

        exceptionMsg.Should().Be("UNIQUE constraint failed: Customers.Code");
    }

    [Fact]
    public void Should_Throw_CannotInsertNullException()
    {
        var repository = Scope.Resolve<IRepository<CustomerEntity, long>>();
        var generator = Scope.Resolve<CustomerGenerator>();
        var entityOne = generator.Generate();
        entityOne.IdCard = null;

        var exception = Assert.Throws<CannotInsertNullException>(() =>
        {
            repository.Create(entityOne, true);
        });

        var exceptionMsg = Regex.Match(exception.InnerException.Message, "(?<=[']).+(?=['])").Value;

        exceptionMsg.Should().Be("NOT NULL constraint failed: Customers.IdCard");
    }

    [Fact]
    public void Should_Throw_ReferenceConstraintException()
    {
        var repository = Scope.Resolve<IRepository<ProductItemEntity, long>>();
        var generator = Scope.Resolve<ProductItemGenerator>();
        var entityOne = generator.Generate();

        var exception = Assert.Throws<ReferenceConstraintException>(() =>
        {
            repository.Create(entityOne, true);
        });

        var exceptionMsg = Regex.Match(exception.InnerException.Message, "(?<=[']).+(?=['])").Value;

        exceptionMsg.Should().Be("FOREIGN KEY constraint failed");
    }
}