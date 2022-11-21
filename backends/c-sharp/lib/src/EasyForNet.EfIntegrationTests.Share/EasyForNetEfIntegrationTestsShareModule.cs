using EasyForNet.EntityFramework;
using EasyForNet.Modules;
using EasyForNet.Tests.Share;

namespace EasyForNet.EfIntegrationTests.Share;

[DependOn(typeof(EasyForNetModule))]
[DependOn(typeof(EasyForNetEntityFrameworkModule))]
[DependOn(typeof(EasyForNetTestsShareModule))]
public class EasyForNetEfIntegrationTestsShareModule : ModuleBase
{
}