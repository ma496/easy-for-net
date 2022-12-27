using Autofac;
using CSharpTemplate.Common;
using CSharpTemplate.Common.Context;
using CSharpTemplate.Data.Context;
using EasyForNet;
using EasyForNet.EntityFramework;
using EasyForNet.Modules;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace CSharpTemplate.Data;

[DependOn(typeof(EasyForNetModule))]
[DependOn(typeof(EasyForNetEntityFrameworkModule))]
[DependOn(typeof(CSharpTemplateCommonModule))]
public class CSharpTemplateDataModule : ModuleBase
{
    public override void Dependencies(ContainerBuilder builder, IConfiguration configuration)
    {
        builder.Register(c =>
        {
            var optionBuilder = new DbContextOptionsBuilder<CSharpTemplateDbContext>();
            optionBuilder.UseSqlServer(configuration.GetConnectionString("DefaultConnection"));
            return new CSharpTemplateDbContext(optionBuilder.Options, c.Resolve<ICurrentUser>());
        }).InstancePerLifetimeScope()
        .AsSelf()
        .As<CSharpTemplateDbContextBase>();
    }
}