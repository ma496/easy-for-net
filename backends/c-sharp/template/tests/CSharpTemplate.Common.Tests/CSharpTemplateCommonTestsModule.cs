using Autofac;
using CSharpTemplate.Common.Identity.Permissions.Provider;
using EasyForNet;
using EasyForNet.Modules;
using EasyForNet.Tests.Share;
using Microsoft.Extensions.Configuration;

namespace CSharpTemplate.Common.Tests;

[DependOn(typeof(EasyForNetModule))]
[DependOn(typeof(EasyForNetTestsShareModule))]
[DependOn(typeof(CSharpTemplateCommonModule))]
public class CSharpTemplateCommonTestsModule : ModuleBase
{
    public override void Dependencies(ContainerBuilder builder, IConfiguration configuration)
    {
        builder.RegisterType<PermissionsContext>()
            .As<IPermissionsContext>()
            .InstancePerLifetimeScope();
    }
}