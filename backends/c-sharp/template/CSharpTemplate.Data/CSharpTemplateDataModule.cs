using EasyForNet;
using EasyForNet.EntityFramework;
using EasyForNet.Modules;

namespace CSharpTemplate.Data;

[DependOn(typeof(EasyForNetModule))]
[DependOn(typeof(EasyForNetEntityFrameworkModule))]
public class CSharpTemplateDataModule : ModuleBase
{
   
}
