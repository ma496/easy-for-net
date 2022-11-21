using System;
using EasyForNet.Domain.Entities;
using EasyForNet.Exceptions.UserFriendly;
using EasyForNet.Tests.Base;
using Xunit;
using Xunit.Abstractions;

namespace EasyForNet.Tests.Domain.Entities;

public class EntityIdValidatorTests : TestsBase
{
    public EntityIdValidatorTests(ITestOutputHelper outputHelper) : base(outputHelper)
    {
    }

    [Theory]
    [InlineData(2, true)]
    [InlineData(434, false)]
    public void Validate(long id, bool isAppError)
    {
        EntityIdValidator.Validate(id, isAppError);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    [InlineData(-2344)]
    public void Validate_ExceptException(long id)
    {
        Assert.Throws<Exception>(() => EntityIdValidator.Validate(id, false));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    [InlineData(-2344)]
    public void Validate_ExceptAppException(long id)
    {
        Assert.Throws<UserFriendlyException>(() => EntityIdValidator.Validate(id));
    }
}