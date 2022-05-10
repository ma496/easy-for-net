using System;
using System.Threading.Tasks;
using EasyForNet.EntityFramework.Tests.Base;
using EasyForNet.EntityFramework.Tests.Data;
using EasyForNet.EntityFramework.Tests.GenerateData;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;
using EasyForNet.Extensions;

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
            customer.Should().BeEquivalentTo(savedCustomer);

            var user = Services.GetRequiredService<ICurrentUser>();

            Assert.Equal(user.Username, savedCustomer.CreatedBy);
            Assert.Equal(user.Username, savedCustomer.UpdatedBy);

            savedCustomer.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(3));
            savedCustomer.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(3));
        }
    }
}