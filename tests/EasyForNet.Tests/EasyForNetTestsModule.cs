using EasyForNet.Modules;
using EasyForNet.Tests.Share;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EasyForNet.Tests
{
    [DependOn(typeof(EasyForNetModule))]
    [DependOn(typeof(EasyForNetTestsShareModule))]
    public class EasyForNetTestsModule : ModuleBase
    {
        public override void Dependencies(IServiceCollection services, IConfiguration configuration)
        {
            services.AddDistributedMemoryCache();
        }
    }
}