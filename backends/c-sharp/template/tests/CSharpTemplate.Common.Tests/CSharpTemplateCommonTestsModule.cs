using EasyForNet;
using EasyForNet.Modules;
using EasyForNet.Tests.Share;

namespace CSharpTemplate.Common.Tests;

[DependOn(typeof(EasyForNetModule))]
[DependOn(typeof(EasyForNetTestsShareModule))]
[DependOn(typeof(CSharpTemplateCommonModule))]
public class CSharpTemplateCommonTestsModule : ModuleBase
{
    
}