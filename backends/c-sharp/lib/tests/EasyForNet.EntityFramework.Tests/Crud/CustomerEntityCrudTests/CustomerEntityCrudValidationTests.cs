using System.Threading.Tasks;
using EasyForNet.EntityFramework.Tests.Base;
using EasyForNet.EntityFramework.Tests.Data.Entities;
using EasyForNet.Exceptions.UserFriendly;
using Xunit;
using Xunit.Abstractions;

namespace EasyForNet.EntityFramework.Tests.Crud.CustomerEntityCrudTests;

public class CustomerEntityCrudValidationTests : CrudTestsBase<CustomerCrudActions, CustomerEntity, long,
    CustomerDto, CustomerDto, CustomerDto, CustomerDto, CustomerDto, CustomerDto>
{
    public CustomerEntityCrudValidationTests(ITestOutputHelper outputHelper) : base(outputHelper)
    {
    }

    protected override CustomerDto NewDto()
    {
        return CustomerEntityCrudTestsHelper.NewCustomer();
    }

    [Fact]
    public async Task Create_EmptyNameTest()
    {
        var exception = await Assert.ThrowsAsync<ValidationException>(async () =>
        {
            await InternalCreateTestAsync(dto => dto.Name = null);
        });

        Assert.Single(exception.ValidationErrors);
        Assert.Equal(nameof(CustomerDto.Name), exception.ValidationErrors[0].PropertyName);
        Assert.Equal($"The {nameof(CustomerDto.Name)} field is required.",
            exception.ValidationErrors[0].ErrorMessage);
    }

    [Fact]
    public async Task Create_EmptyIdCardTest()
    {
        var exception = await Assert.ThrowsAsync<ValidationException>(async () =>
        {
            await InternalCreateTestAsync(dto => dto.IdCard = null);
        });

        Assert.Single(exception.ValidationErrors);
        Assert.Equal(nameof(CustomerDto.IdCard), exception.ValidationErrors[0].PropertyName);
        Assert.Equal("'Id Card' must not be empty.", exception.ValidationErrors[0].ErrorMessage);
    }

    [Fact]
    public async Task Create_EmptyCellNoTest()
    {
        var exception = await Assert.ThrowsAsync<ValidationException>(async () =>
        {
            await InternalCreateTestAsync(dto => dto.CellNo = null);
        });

        Assert.Single(exception.ValidationErrors);
        Assert.Equal(nameof(CustomerDto.CellNo), exception.ValidationErrors[0].PropertyName);
        Assert.Equal("'Cell No' must not be empty.", exception.ValidationErrors[0].ErrorMessage);
    }

    [Fact]
    public async Task Update_EmptyNameTest()
    {
        var exception = await Assert.ThrowsAsync<ValidationException>(async () =>
        {
            await InternalUpdateTestAsync(dto => dto.Name = null);
        });

        Assert.Single(exception.ValidationErrors);
        Assert.Equal(nameof(CustomerDto.Name), exception.ValidationErrors[0].PropertyName);
        Assert.Equal($"The {nameof(CustomerDto.Name)} field is required.",
            exception.ValidationErrors[0].ErrorMessage);
    }

    [Fact]
    public async Task Update_EmptyIdCardTest()
    {
        var exception = await Assert.ThrowsAsync<ValidationException>(async () =>
        {
            await InternalUpdateTestAsync(dto => dto.IdCard = null);
        });

        Assert.Single(exception.ValidationErrors);
        Assert.Equal(nameof(CustomerDto.IdCard), exception.ValidationErrors[0].PropertyName);
        Assert.Equal("'Id Card' must not be empty.", exception.ValidationErrors[0].ErrorMessage);
    }

    [Fact]
    public async Task Update_EmptyCellNoTest()
    {
        var exception = await Assert.ThrowsAsync<ValidationException>(async () =>
        {
            await InternalUpdateTestAsync(dto => dto.CellNo = null);
        });

        Assert.Single(exception.ValidationErrors);
        Assert.Equal(nameof(CustomerDto.CellNo), exception.ValidationErrors[0].PropertyName);
        Assert.Equal("'Cell No' must not be empty.", exception.ValidationErrors[0].ErrorMessage);
    }
}