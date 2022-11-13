using EasyForNet.Tests.Share.Common;
using Microsoft.Extensions.DependencyInjection;

namespace EasyForNet.Tests.Base
{
    public class EasyForNetTestsFixture : TestsFixtureCommon<EasyForNetTestsModule>
    {
        protected override void AddServices(IServiceCollection services)
        {
            base.AddServices(services);
            services.AddDistributedMemoryCache();
        }
    }
}