using CSharpTemplate.App;
using EasyForNet;
using EasyForNet.EntityFramework;
using EasyForNet.Modules;

namespace CSharpTemplate.Api;

[DependOn(typeof(EasyForNetModule))]
[DependOn(typeof(EasyForNetEntityFrameworkModule))]
[DependOn(typeof(CSharpTemplateAppModule))]
public class CSharpTemplateApiModule : ModuleBase
{
}
