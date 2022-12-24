using CSharpTemplate.Common;
using EasyForNet;
using EasyForNet.EntityFramework;
using EasyForNet.Modules;

namespace CSharpTemplate.Data;

[DependOn(typeof(EasyForNetModule))]
[DependOn(typeof(EasyForNetEntityFrameworkModule))]
[DependOn(typeof(CSharpTemplateCommonModule))]
public class CSharpTemplateDataModule : ModuleBase
{
    
}