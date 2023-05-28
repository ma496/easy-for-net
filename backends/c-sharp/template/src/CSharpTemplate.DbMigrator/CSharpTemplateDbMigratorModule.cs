using Autofac;
using CSharpTemplate.App;
using CSharpTemplate.Common.Identity.Permissions.Provider;
using EasyForNet;
using EasyForNet.EntityFramework;
using EasyForNet.Modules;
using Microsoft.Extensions.Configuration;

namespace CSharpTemplate.DbMigrator;

[DependOn(typeof(EasyForNetModule))]
[DependOn(typeof(EasyForNetEntityFrameworkModule))]
[DependOn(typeof(CSharpTemplateAppModule))]
public class CSharpTemplateDbMigratorModule : ModuleBase
{
    public override void Dependencies(ContainerBuilder builder, IConfiguration configuration)
    {
        builder.RegisterType<PermissionsContext>()
            .As<IPermissionsContext>()
            .SingleInstance();
    }
}