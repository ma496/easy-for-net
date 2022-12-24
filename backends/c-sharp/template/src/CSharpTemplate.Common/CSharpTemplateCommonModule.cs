using EasyForNet;
using EasyForNet.EntityFramework;
using EasyForNet.Modules;

namespace CSharpTemplate.Common;

[DependOn(typeof(EasyForNetModule))]
[DependOn(typeof(EasyForNetEntityFrameworkModule))]
public class CSharpTemplateCommonModule : ModuleBase
{
    
}