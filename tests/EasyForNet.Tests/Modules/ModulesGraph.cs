using EasyForNet.Modules;

namespace EasyForNet.Tests.Modules
{
    public class OneModule : ModuleBase
    {
    }

    [DependOn(typeof(OneModule))]
    public class TwoModule : ModuleBase
    {
    }

    [DependOn(typeof(OneModule))]
    [DependOn(typeof(TwoModule))]
    public class ThreeModule : ModuleBase
    {
    }

    [DependOn(typeof(ThreeModule))]
    public class FourModule : ModuleBase
    {
    }
}