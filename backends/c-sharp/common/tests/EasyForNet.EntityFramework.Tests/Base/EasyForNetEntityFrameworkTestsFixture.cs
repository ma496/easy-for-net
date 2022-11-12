using EasyForNet.EntityFramework.Tests.Data;
using EasyForNet.Tests.Share;
using EasyForNet.Tests.Share.Common;
using Microsoft.Extensions.DependencyInjection;

namespace EasyForNet.EntityFramework.Tests.Base
{
    public class EasyForNetEntityFrameworkTestsFixture : TestsFixtureCommon<EasyForNetEntityFrameworkTestsModule>
    {
        public EasyForNetEntityFrameworkTestsFixture()
        {
            using var scope = GlobalObjects.ServiceProvider.CreateScope();
            var scopeServiceProvider = scope.ServiceProvider;

            var dbContext = scopeServiceProvider.GetRequiredService<EasyForNetEntityFrameworkTestsDb>();
            dbContext.Database.EnsureDeletedAsync().Wait();
            dbContext.Database.EnsureCreatedAsync().Wait();
        }
    }
}