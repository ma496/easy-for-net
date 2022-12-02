using Autofac;
using EasyForNet.Entities;
using EasyForNet.EntityFramework.Repository;
using EasyForNet.EntityFramework.Setting;
using EasyForNet.Modules;
using EasyForNet.Repository;
using EasyForNet.Setting;
using Microsoft.Extensions.Configuration;

namespace EasyForNet.EntityFramework;

[DependOn(typeof(EasyForNetModule))]
public class EasyForNetEntityFrameworkModule : ModuleBase
{
    public override void Dependencies(ContainerBuilder builder, IConfiguration configuration)
    {
        builder.RegisterGeneric(typeof(EfRepository<,>))
            .As(typeof(IRepository<,>))
            .AsSelf()
            .InstancePerDependency();

        builder.RegisterGeneric(typeof(EfRepository<>))
            .As(typeof(IRepository<>))
            .AsSelf()
            .InstancePerDependency();

        builder.RegisterType<EfSettingStore<EfnSettingEntity>>()
            .As<ISettingStore<EfnSettingEntity>>()
            .InstancePerDependency();
    }
}