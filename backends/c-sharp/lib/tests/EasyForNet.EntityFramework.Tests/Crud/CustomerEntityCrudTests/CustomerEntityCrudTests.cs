using System.Threading.Tasks;
using EasyForNet.EntityFramework.Tests.Base;
using EasyForNet.EntityFramework.Tests.Data.Entities;
using Xunit;
using Xunit.Abstractions;

namespace EasyForNet.EntityFramework.Tests.Crud.CustomerEntityCrudTests;

public class CustomerEntityCrudTests : CrudTestsBase<CustomerCrudActions, CustomerEntity, long, CustomerDto,
    CustomerDto, CustomerDto, CustomerDto, CustomerDto, CustomerDto>
{
    public CustomerEntityCrudTests(ITestOutputHelper outputHelper) : base(outputHelper)
    {
    }

    protected override CustomerDto NewDto()
    {
        return CustomerEntityCrudTestsHelper.NewCustomer();
    }

    [Fact]
    public async Task CreateTest()
    {
        await InternalCreateTestAsync();
    }

    [Fact]
    public async Task Create_WithDuplicateTest()
    {
        await InternalCreateDuplicateTest(2, 2, _ => nameof(CustomerDto.Code),
            dto =>
            {
                dto.Code = IncrementalId.Id;
                return nameof(CustomerDto.IdCard);
            });
    }

    [Fact]
    public async Task UpdateTest()
    {
        await InternalUpdateTestAsync(dto =>
        {
            dto.IdCard = $"94545-48633-5455{IncrementalId.Id}";
            dto.Name = "Muhammad Usman";
        });
    }

    [Fact]
    public async Task Update_WithDuplicateTest()
    {
        await InternalUpdateDuplicateTest(2, 2, (dto, dtos) =>
        {
            dto.Code = dtos[1].Code;
            return nameof(CustomerDto.Code);
        }, (dto, dtos) =>
        {
            dto.IdCard = dtos[0].IdCard;
            return nameof(CustomerDto.IdCard);
        });
    }

    [Fact]
    public async Task DeleteTest()
    {
        await InternalDeleteTestAsync();
    }

    [Fact]
    public async Task UndoDeleteTest()
    {
        await InternalUndoDeleteTestAsync();
    }

    [Fact]
    public async Task ListTest()
    {
        await InternalListTestAsync();
    }

    [Fact]
    public async Task GetTest()
    {
        await InternalGetTestAsync();
    }
}