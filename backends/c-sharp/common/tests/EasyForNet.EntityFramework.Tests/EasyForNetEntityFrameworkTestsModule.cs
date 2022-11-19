using Autofac;
using EasyForNet.Entities;
using EasyForNet.EntityFramework.Repository;
using EasyForNet.EntityFramework.Setting;
using EasyForNet.EntityFramework.Tests.Data;
using EasyForNet.EntityFramework.Tests.Repository;
using EasyForNet.Modules;
using EasyForNet.Repository;
using EasyForNet.Setting;
using EasyForNet.Tests.Share;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace EasyForNet.EntityFramework.Tests
{
    [DependOn(typeof(EasyForNetModule))]
    [DependOn(typeof(EasyForNetEntityFrameworkModule))]
    [DependOn(typeof(EasyForNetTestsShareModule))]
    public class EasyForNetEntityFrameworkTestsModule : ModuleBase
    {
        public override void Dependencies(ContainerBuilder builder, IConfiguration configuration)
        {
            builder.Register(sp =>
            {
                var options = new DbContextOptionsBuilder()
                    .UseSqlite("Data Source = EasyForNetEntityFrameworkTests.db")
                    .EnableSensitiveDataLogging()
                    .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
                    .Options;
                var currentUser = sp.Resolve<ICurrentUser>();
                var db = new EasyForNetEntityFrameworkTestsDb(options, currentUser);
                return db;
            }).InstancePerLifetimeScope();

            builder.RegisterType<SettingStore<EasyForNetEntityFrameworkTestsDb, EfnSettingEntity>>()
                .As<ISettingStore<EfnSettingEntity>>()
                .InstancePerLifetimeScope();

            builder.RegisterGeneric(typeof(EasyForNetRepository<,>))
                .As(typeof(IRepository<,>))
                .InstancePerLifetimeScope();
        }
    }
}