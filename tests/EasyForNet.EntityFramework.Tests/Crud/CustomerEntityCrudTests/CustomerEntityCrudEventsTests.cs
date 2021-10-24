using System.Linq;
using System.Threading.Tasks;
using EasyForNet.EntityFramework.Tests.Base;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace EasyForNet.EntityFramework.Tests.Crud.CustomerEntityCrudTests
{
    public class CustomerEntityCrudEventsTests : TestsBase
    {
        public CustomerEntityCrudEventsTests(ITestOutputHelper outputHelper) : base(outputHelper)
        {
        }
        
        [Fact]
        public async Task CreateTest()
        {
            var crudActions = Services.GetRequiredService<CustomerCrudActions>();
            
            var dto = CustomerEntityCrudTestsHelper.NewCustomer();
            await crudActions.CreateAsync(dto);

            Assert.True(crudActions.IsBeforeCreate);
            Assert.True(crudActions.IsAfterCreate);
        }

        [Fact]
        public async Task UpdateTest()
        {
            var crudActions = Services.GetRequiredService<CustomerCrudActions>();

            var dto = CustomerEntityCrudTestsHelper.NewCustomer();
            dto = await crudActions.CreateAsync(dto);

            crudActions = NewScopeService<CustomerCrudActions>();

            var createdDto = await crudActions.GetAsync(dto.Id);
            
            CompareAssert(dto, createdDto);
            
            await crudActions.UpdateAsync(dto.Id, createdDto);

            Assert.True(crudActions.IsBeforeUpdate);
            Assert.True(crudActions.IsAfterUpdate);
        }

        [Fact]
        public async Task DeleteTest()
        {
            var crudActions = Services.GetRequiredService<CustomerCrudActions>();

            var dto = CustomerEntityCrudTestsHelper.NewCustomer();
            dto = await crudActions.CreateAsync(dto);

            crudActions = NewScopeService<CustomerCrudActions>();

            var createdDto = await crudActions.GetAsync(dto.Id);
            
            CompareAssert(dto, createdDto);

            await crudActions.DeleteAsync(dto.Id);

            Assert.True(crudActions.IsBeforeDelete);
            Assert.True(crudActions.IsAfterDelete);
        }
        
        [Fact]
        public async Task UndoDeleteTest()
        {
            var crudActions = Services.GetRequiredService<CustomerCrudActions>();

            var dto = CustomerEntityCrudTestsHelper.NewCustomer();
            dto = await crudActions.CreateAsync(dto);

            crudActions = NewScopeService<CustomerCrudActions>();

            var createdDto = await crudActions.GetAsync(dto.Id);
            
            CompareAssert(dto, createdDto);

            await crudActions.DeleteAsync(dto.Id);

            crudActions = NewScopeService<CustomerCrudActions>();

            var deletedDto = await crudActions.GetAsync(dto.Id);
            
            Assert.Null(deletedDto);

            await crudActions.UndoDeleteAsync(dto.Id);
            
            Assert.True(crudActions.IsBeforeUndoDelete);
            Assert.True(crudActions.IsAfterUndoDelete);
        }

        [Fact]
        public async Task ListTest()
        {
            var crudActions = Services.GetRequiredService<CustomerCrudActions>();

            var dtos = CustomerEntityCrudTestsHelper.NewCustomers(10);
            dtos = dtos.Select(d => crudActions.CreateAsync(d).Result).ToList();

            crudActions = NewScopeService<CustomerCrudActions>();

            var createdDtos = (await crudActions.ListAsync())
                .Where(o => dtos.Select(d => d.Id).Contains(o.Id))
                .ToList();
            
            CompareAssert(dtos, createdDtos);

            Assert.True(crudActions.IsBeforeList);
        }

        [Fact]
        public async Task GetTest()
        {
            var crudActions = Services.GetRequiredService<CustomerCrudActions>();

            var dto = CustomerEntityCrudTestsHelper.NewCustomer();
            dto = await crudActions.CreateAsync(dto);

            crudActions = NewScopeService<CustomerCrudActions>();

            var createdDto = await crudActions.GetAsync(dto.Id);
            
            CompareAssert(dto, createdDto);

            Assert.True(crudActions.IsBeforeGet);
        }
    }
}