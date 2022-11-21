using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using EasyForNet.Modules;
using EasyForNet.Tests.Base;
using Xunit;
using Xunit.Abstractions;

namespace EasyForNet.Tests.Modules;

[SuppressMessage("ReSharper", "CoVariantArrayConversion")]
public class ModuleTests : TestsBase
{
    public ModuleTests(ITestOutputHelper outputHelper) : base(outputHelper)
    {
    }

    [Fact]
    public void GetModulesTest()
    {
        var modulesInfo = GetModulesInfo();
        Assert.NotNull(modulesInfo);

        Assert.Equal(5, modulesInfo.Count);

        Assert.Equal(typeof(FourModule), modulesInfo[0].Module.GetType());
        Assert.Equal(0, modulesInfo[0].Level);

        Assert.Equal(typeof(ThreeModule), modulesInfo[1].Module.GetType());
        Assert.Equal(1, modulesInfo[1].Level);

        Assert.Equal(typeof(OneModule), modulesInfo[2].Module.GetType());
        Assert.Equal(2, modulesInfo[2].Level);

        Assert.Equal(typeof(TwoModule), modulesInfo[3].Module.GetType());
        Assert.Equal(2, modulesInfo[3].Level);

        Assert.Equal(typeof(OneModule), modulesInfo[4].Module.GetType());
        Assert.Equal(3, modulesInfo[4].Level);
    }

    [Fact]
    public void GetUniqueModulesTest()
    {
        var modulesInfo = GetUniqueAndOrderModulesInfo();

        Assert.Equal(4, modulesInfo.Count);

        Assert.Equal(typeof(OneModule), modulesInfo[0].Module.GetType());
        Assert.Equal(3, modulesInfo[0].Level);

        Assert.Equal(typeof(TwoModule), modulesInfo[1].Module.GetType());
        Assert.Equal(2, modulesInfo[1].Level);

        Assert.Equal(typeof(ThreeModule), modulesInfo[2].Module.GetType());
        Assert.Equal(1, modulesInfo[2].Level);

        Assert.Equal(typeof(FourModule), modulesInfo[3].Module.GetType());
        Assert.Equal(0, modulesInfo[3].Level);
    }

    private static List<ModuleInfo> GetModulesInfo()
    {
        var type = typeof(AppInitializer);

        var getModulesFunc = type.GetMethod("GetModulesInfo", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(getModulesFunc);
        var modules = getModulesFunc.Invoke(null, new object[] {typeof(FourModule), 0}) as List<ModuleInfo>;

        return modules;
    }

    private static List<ModuleInfo> GetUniqueAndOrderModulesInfo()
    {
        var modules = GetModulesInfo();
        var type = typeof(AppInitializer);

        var getUniqueAndOrderModulesFunc = type.GetMethod("GetUniqueAndOrderModulesInfo",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(getUniqueAndOrderModulesFunc);
        var uniqueAndOrderModules = getUniqueAndOrderModulesFunc.Invoke(null, new[] {modules}) as List<ModuleInfo>;

        return uniqueAndOrderModules;
    }
}