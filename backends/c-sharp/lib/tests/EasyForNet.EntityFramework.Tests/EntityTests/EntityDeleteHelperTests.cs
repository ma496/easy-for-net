using System.Threading.Tasks;
using Autofac;
using EasyForNet.EntityFramework.Helpers;
using EasyForNet.EntityFramework.Tests.Base;
using EasyForNet.EntityFramework.Tests.Data;
using EasyForNet.EntityFramework.Tests.Data.Entities;
using EasyForNet.EntityFramework.Tests.GenerateData;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace EasyForNet.EntityFramework.Tests.EntityTests;

public class EntityDeleteHelperTests : TestsBase
{
    public EntityDeleteHelperTests(ITestOutputHelper outputHelper) : base(outputHelper)
    {
    }

    [Fact]
    public async Task SoftDeleteTest()
    {
        var dbContext = Scope.Resolve<EasyForNetEntityFrameworkTestsDb>();
        var generator = NewScopeService<CustomerGenerator>();

        var customerEntity = await generator.GenerateAndSaveAsync();
        EntityDeleteHelper
            .Delete<EasyForNetEntityFrameworkTestsDb, CustomerEntity, long>(dbContext, customerEntity);

        await dbContext.SaveChangesAsync();

        dbContext = NewScopeService<EasyForNetEntityFrameworkTestsDb>();

        var savedCustomer = await dbContext.Customers.FindAsync(customerEntity.Id);

        Assert.Null(savedCustomer);
    }

    [Fact]
    public async Task DeleteTest()
    {
        var dbContext = Scope.Resolve<EasyForNetEntityFrameworkTestsDb>();
        var tagEntity = new TagEntity {Name = $"{IncrementalId.Id}_mobile"};

        await dbContext.Tags.AddAsync(tagEntity);
        await dbContext.SaveChangesAsync();

        dbContext = NewScopeService<EasyForNetEntityFrameworkTestsDb>();

        EntityDeleteHelper.Delete<EasyForNetEntityFrameworkTestsDb, TagEntity, long>(dbContext, tagEntity);
        await dbContext.SaveChangesAsync();

        dbContext = NewScopeService<EasyForNetEntityFrameworkTestsDb>();

        var savedTagEntity = await dbContext.Tags.FindAsync(tagEntity.Id);

        Assert.Null(savedTagEntity);
    }
}