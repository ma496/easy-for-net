using Autofac;
using EasyForNet.EntityFramework.Tests.Data;
using EasyForNet.Tests.Share;
using EasyForNet.Tests.Share.Common;
using Microsoft.Extensions.DependencyInjection;

namespace EasyForNet.EntityFramework.Tests.Base;

public class EasyForNetEntityFrameworkTestsFixture : TestsFixtureCommon<EasyForNetEntityFrameworkTestsModule>
{
    public EasyForNetEntityFrameworkTestsFixture()
    {
        using var scope = GlobalObjects.Container.BeginLifetimeScope();

        var dbContext = scope.Resolve<EasyForNetEntityFrameworkTestsDb>();
        dbContext.Database.EnsureDeletedAsync().Wait();
        dbContext.Database.EnsureCreatedAsync().Wait();
    }

    protected override void AddServices(IServiceCollection services)
    {
        base.AddServices(services);
        services.AddDistributedMemoryCache();
    }
}