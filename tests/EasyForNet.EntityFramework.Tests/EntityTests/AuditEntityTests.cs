using System;
using System.Threading.Tasks;
using EasyForNet.EntityFramework.Tests.Base;
using EasyForNet.EntityFramework.Tests.Data;
using EasyForNet.EntityFramework.Tests.GenerateData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace EasyForNet.EntityFramework.Tests.EntityTests
{
    public class AuditEntityTests : TestsBase
    {
        public AuditEntityTests(ITestOutputHelper outputHelper) : base(outputHelper)
        {
        }

        [Fact]
        public async Task AuditEntityTest()
        {
            var dbContext = Services.GetRequiredService<EasyForNetEntityFrameworkTestsDb>();

            var customer = await NewScopeService<CustomerGenerator>().GenerateAndSaveAsync();

            var savedCustomer = await dbContext.Customers.SingleOrDefaultAsync(c => c.Id == customer.Id);

            Assert.NotNull(savedCustomer);
            CompareAssert(customer, savedCustomer);

            var user = Services.GetRequiredService<ICurrentUser>();

            Assert.Equal(user.Username, savedCustomer.CreatedBy);
            Assert.Equal(user.Username, savedCustomer.UpdatedBy);

            CompareObject.Config.MaxMillisecondsDateDifference = 10000;
            CompareAssert(savedCustomer.CreatedAt, DateTime.UtcNow);
            CompareAssert(savedCustomer.UpdatedAt, DateTime.UtcNow);
        }
    }
}