using Autofac;
using CSharpTemplate.App;
using CSharpTemplate.Common;
using CSharpTemplate.Common.Identity.Permissions.Provider;
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
    public override void Dependencies(ContainerBuilder builder, IConfiguration configuration)
    {
        builder.RegisterType<PermissionsContext>()
            .As<IPermissionsContext>()
            .SingleInstance();
    }
}
