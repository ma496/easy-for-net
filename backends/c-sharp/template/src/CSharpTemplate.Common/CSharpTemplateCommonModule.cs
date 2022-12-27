using Autofac;
using CSharpTemplate.Common.Identity.Entities;
using EasyForNet;
using EasyForNet.EntityFramework;
using EasyForNet.Modules;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;

namespace CSharpTemplate.Common;

[DependOn(typeof(EasyForNetModule))]
[DependOn(typeof(EasyForNetEntityFrameworkModule))]
public class CSharpTemplateCommonModule : ModuleBase
{
    public override void Dependencies(ContainerBuilder builder, IConfiguration configuration)
    {
        builder.RegisterType<PasswordHasher<AppUser>>()
            .As<IPasswordHasher<AppUser>>()
            .InstancePerDependency();

    }
}