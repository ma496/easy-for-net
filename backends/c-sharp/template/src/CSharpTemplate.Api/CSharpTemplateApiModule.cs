using CSharpTemplate.App;
using CSharpTemplate.Common;
using EasyForNet;
using EasyForNet.EntityFramework;
using EasyForNet.Modules;

namespace CSharpTemplate.Api;

[DependOn(typeof(EasyForNetModule))]
[DependOn(typeof(EasyForNetEntityFrameworkModule))]
[DependOn(typeof(CSharpTemplateCommonModule))]
[DependOn(typeof(CSharpTemplateAppModule))]
public class CSharpTemplateApiModule : ModuleBase
{
}
