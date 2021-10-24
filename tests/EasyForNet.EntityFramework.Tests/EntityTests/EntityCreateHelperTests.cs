using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using AutoMapper;
using EasyForNet.EntityFramework.Helpers;
using EasyForNet.EntityFramework.Tests.Base;
using EasyForNet.EntityFramework.Tests.Data;
using EasyForNet.EntityFramework.Tests.Data.Entities;
using EasyForNet.Exceptions;
using EasyForNet.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace EasyForNet.EntityFramework.Tests.EntityTests
{
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Local")]
    public class EntityCreateHelperTests : TestsBase
    {
        public EntityCreateHelperTests(ITestOutputHelper outputHelper) : base(outputHelper)
        {
        }

        [Fact]
        public async Task CreateTest()
        {
            var dbContext = Services.GetRequiredService<EasyForNetEntityFrameworkTestsDb>();
            var mapper = Services.GetRequiredService<IMapper>();

            var customerDto = new CustomerDto
            {
                Code = IncrementalId.Id,
                IdCard = $"{IncrementalId.Id}432-5442-5986",
                CellNo = "03434234234",
                Name = "Muhammad Ali"
            };

            var customerEntity = mapper.Map<CustomerEntity>(customerDto);

            await EntityCreateHelper.AddAsync<EasyForNetEntityFrameworkTestsDb, CustomerEntity, long>(dbContext, customerEntity, e => e.Code);

            await dbContext.SaveChangesAsync();

            dbContext = NewScopeService<EasyForNetEntityFrameworkTestsDb>();
            
            var savedCustomerEntity = await dbContext.Customers.FindAsync(customerEntity.Id);

            Assert.NotNull(savedCustomerEntity);
            CompareAssert(customerEntity, savedCustomerEntity);
        }

        [Fact]
        public async Task Create_UniqueCodeTest()
        {
            var exception = await Assert.ThrowsAsync<UniquePropertyException>(async () =>
            {
                var dbContext = Services.GetRequiredService<EasyForNetEntityFrameworkTestsDb>();
                var mapper = Services.GetRequiredService<IMapper>();

                var customerDto = new CustomerDto
                {
                    Code = IncrementalId.Id,
                    IdCard = $"{IncrementalId.Id}344-34324-6554",
                    CellNo = "03434234234",
                    Name = "Muhammad Ali"
                };

                var customerEntity = mapper.Map<CustomerEntity>(customerDto);

                await EntityCreateHelper.AddAsync<EasyForNetEntityFrameworkTestsDb, CustomerEntity, long>(dbContext, customerEntity, e => e.Code, e => e.IdCard);

                await dbContext.SaveChangesAsync();

                customerEntity = customerEntity.Clone();
                customerEntity.IdCard = $"{IncrementalId.Id}3469-98666-8568";

                dbContext = NewScopeService<EasyForNetEntityFrameworkTestsDb>();

                await EntityCreateHelper.AddAsync<EasyForNetEntityFrameworkTestsDb, CustomerEntity, long>(dbContext, customerEntity, e => e.Code, e => e.IdCard);

                await dbContext.SaveChangesAsync();
            });

            Assert.Equal($"Duplicate of {nameof(CustomerDto.Code)} not allowed", exception.Message);
        }

        [Fact]
        public async Task Create_UniqueIdCardTest()
        {
            var exception = await Assert.ThrowsAsync<UniquePropertyException>(async () =>
            {
                var dbContext = Services.GetRequiredService<EasyForNetEntityFrameworkTestsDb>();
                var mapper = Services.GetRequiredService<IMapper>();

                var customerDto = new CustomerDto
                {
                    Code = IncrementalId.Id,
                    IdCard = $"{IncrementalId.Id}344-65443-656433",
                    CellNo = "03434234234",
                    Name = "Muhammad Ali"
                };

                var customerEntity = mapper.Map<CustomerEntity>(customerDto);

                await EntityCreateHelper.AddAsync<EasyForNetEntityFrameworkTestsDb, CustomerEntity, long>(dbContext, customerEntity, e => e.Code, e => e.IdCard);

                await dbContext.SaveChangesAsync();

                customerEntity = customerEntity.Clone();
                customerEntity.Code = IncrementalId.Id;

                dbContext = NewScopeService<EasyForNetEntityFrameworkTestsDb>();

                await EntityCreateHelper.AddAsync<EasyForNetEntityFrameworkTestsDb, CustomerEntity, long>(dbContext, customerEntity, e => e.Code, e => e.IdCard);

                await dbContext.SaveChangesAsync();
            });

            Assert.Equal($"Duplicate of {nameof(CustomerDto.IdCard)} not allowed", exception.Message);
        }

        [Fact]
        public async Task Create_SoftDelete_UniqueTest()
        {
            var dbContext = Services.GetRequiredService<EasyForNetEntityFrameworkTestsDb>();
            var mapper = Services.GetRequiredService<IMapper>();

            var customerDto = new CustomerDto
            {
                Code = IncrementalId.Id,
                IdCard = $"{IncrementalId.Id}344-34324-6554",
                CellNo = "03434234234",
                Name = "Muhammad Ali"
            };

            var customerEntity = mapper.Map<CustomerEntity>(customerDto);

            await EntityCreateHelper.AddAsync<EasyForNetEntityFrameworkTestsDb, CustomerEntity, long>(dbContext, customerEntity, e => e.Code, e => e.IdCard);

            await dbContext.SaveChangesAsync();

            customerEntity = customerEntity.Clone();
            customerEntity.IsDeleted = true;

            dbContext = NewScopeService<EasyForNetEntityFrameworkTestsDb>();
            
            dbContext.Customers.Update(customerEntity);

            await dbContext.SaveChangesAsync();

            dbContext = NewScopeService<EasyForNetEntityFrameworkTestsDb>();

            var newCustomerEntity = mapper.Map<CustomerEntity>(customerDto);

            await Assert.ThrowsAsync<UniquePropertyDeletedException>(async () =>
            {
                await EntityCreateHelper.AddAsync<EasyForNetEntityFrameworkTestsDb, CustomerEntity, long>(dbContext, newCustomerEntity, e => e.Code, e => e.IdCard);
            });
        }

        [AutoMap(typeof(CustomerEntity), ReverseMap = true)]
        public class CustomerDto
        {
            public long Code { get; set; }

            public string IdCard { get; set; }

            public string Name { get; set; }

            public string CellNo { get; set; }
        }
    }
}