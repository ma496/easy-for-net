using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using AutoMapper;
using EasyForNet.Application.Dto;
using EasyForNet.EntityFramework.Helpers;
using EasyForNet.EntityFramework.Tests.Base;
using EasyForNet.EntityFramework.Tests.Data;
using EasyForNet.EntityFramework.Tests.Data.Entities;
using EasyForNet.EntityFramework.Tests.GenerateData;
using EasyForNet.Exceptions.UserFriendly;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace EasyForNet.EntityFramework.Tests.EntityTests
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Local")]
    public class EntityUpdateHelperTests : TestsBase
    {
        public EntityUpdateHelperTests(ITestOutputHelper outputHelper) : base(outputHelper)
        {
        }

        [Fact]
        public async Task Update_WithSameIdCardTest()
        {
            var dbContext = Services.GetRequiredService<EasyForNetEntityFrameworkTestsDb>();
            var customerGenerator = NewScopeService<CustomerGenerator>();

            var newCustomer = await customerGenerator.GenerateAndSaveAsync();
            var customerDto = Mapper.Map<CustomerDto>(newCustomer);
            customerDto.Name = "Thomson";

            var customerForUpdate =
                await QueryHelper.GetEntityAsync<CustomerEntity, long>(dbContext.Customers, customerDto.Id);
            Mapper.Map(customerDto, customerForUpdate);

            await EntityUpdateHelper.UpdateAsync<EasyForNetEntityFrameworkTestsDb, CustomerEntity, long>(dbContext,
                customerForUpdate, c => c.IdCard);
            await dbContext.SaveChangesAsync();

            dbContext = NewScopeService<EasyForNetEntityFrameworkTestsDb>();

            var updatedCustomer = await dbContext.Customers.FindAsync(customerForUpdate.Id);

            Assert.NotNull(updatedCustomer);
            customerForUpdate.Should().BeEquivalentTo(updatedCustomer);
        }

        [Fact]
        public async Task Update_WithDifferentIdCardTest()
        {
            var dbContext = Services.GetRequiredService<EasyForNetEntityFrameworkTestsDb>();
            var customerGenerator = NewScopeService<CustomerGenerator>();

            var newCustomer = await customerGenerator.GenerateAndSaveAsync();
            var customerDto = Mapper.Map<CustomerDto>(newCustomer);
            customerDto.IdCard = $"{IncrementalId.Id}432-5678-8589-7548";
            customerDto.Name = "Jhon";

            var customerForUpdate =
                await QueryHelper.GetEntityAsync<CustomerEntity, long>(dbContext.Customers, customerDto.Id);
            Mapper.Map(customerDto, customerForUpdate);

            await EntityUpdateHelper.UpdateAsync<EasyForNetEntityFrameworkTestsDb, CustomerEntity, long>(dbContext,
                customerForUpdate, c => c.IdCard);
            await dbContext.SaveChangesAsync();

            dbContext = NewScopeService<EasyForNetEntityFrameworkTestsDb>();

            var updatedCustomer = await dbContext.Customers.FindAsync(customerForUpdate.Id);

            Assert.NotNull(updatedCustomer);
            customerForUpdate.Should().BeEquivalentTo(updatedCustomer);
        }

        [Fact]
        public async Task Update_UniqueTest()
        {
            await Assert.ThrowsAsync<UniquePropertyException>(async () =>
            {
                var dbContext = Services.GetRequiredService<EasyForNetEntityFrameworkTestsDb>();
                var customerGenerator = NewScopeService<CustomerGenerator>();

                var newCustomers = await customerGenerator.GenerateAndSaveAsync(2);
                var customerDto = Mapper.Map<CustomerDto>(newCustomers[1]);
                customerDto.IdCard = newCustomers[0].IdCard;

                var customerForUpdate =
                    await QueryHelper.GetEntityAsync<CustomerEntity, long>(dbContext.Customers, customerDto.Id);
                Mapper.Map(customerDto, customerForUpdate);

                dbContext = NewScopeService<EasyForNetEntityFrameworkTestsDb>();

                await EntityUpdateHelper.UpdateAsync<EasyForNetEntityFrameworkTestsDb, CustomerEntity, long>(dbContext,
                    customerForUpdate, c => c.IdCard);
                await dbContext.SaveChangesAsync();
            });
        }

        [Fact]
        public async Task Update_SoftDelete_UniqueTest()
        {
            var dbContext = Services.GetRequiredService<EasyForNetEntityFrameworkTestsDb>();
            var customerGenerator = NewScopeService<CustomerGenerator>();

            var newCustomers = await customerGenerator.GenerateAndSaveAsync(2);

            newCustomers[0].IsDeleted = true;
            dbContext.Customers.Update(newCustomers[0]);
            await dbContext.SaveChangesAsync();

            var customerDto = Mapper.Map<CustomerDto>(newCustomers[1]);
            customerDto.IdCard = newCustomers[0].IdCard;

            var customerEntity = await QueryHelper.GetEntityAsync(dbContext.Customers, customerDto.Id);
            Mapper.Map(customerDto, customerEntity);

            dbContext = NewScopeService<EasyForNetEntityFrameworkTestsDb>();

            await Assert.ThrowsAsync<UniquePropertyDeletedException>(async () =>
            {
                await EntityUpdateHelper.UpdateAsync<EasyForNetEntityFrameworkTestsDb, CustomerEntity, long>(
                    dbContext, customerEntity, c => c.IdCard);
            });
        }

        [AutoMap(typeof(CustomerEntity), ReverseMap = true)]
        public class CustomerDto : Dto<long>
        {
            public string IdCard { get; set; }

            public string Name { get; set; }

            public string CellNo { get; set; }
        }
    }
}