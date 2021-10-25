using EasyForNet.EntityFramework;
using EasyForNet.Modules;
using EasyForNet.Tests.Share;

namespace EasyForNet.EfIntegrationTests.Share
{
    [DependOn(typeof(EasyForNetModule))]
    [DependOn(typeof(EasyForNetFrameworkModule))]
    [DependOn(typeof(EasyForNetTestsShareModule))]
    public class EasyForNetEfIntegrationTestsShareModule : ModuleBase
    {
    }
}