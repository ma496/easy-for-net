using CSharpTemplate.Data;
using EasyForNet;
using EasyForNet.EntityFramework;
using EasyForNet.Modules;

namespace CSharpTemplate.App;

[DependOn(typeof(EasyForNetModule))]
[DependOn(typeof(EasyForNetEntityFrameworkModule))]
[DependOn(typeof(CSharpTemplateDataModule))]
public class CSharpTemplateAppModule : ModuleBase
{
}