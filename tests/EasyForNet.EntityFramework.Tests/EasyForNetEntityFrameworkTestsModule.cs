using EasyForNet.EntityFramework.Tests.Data;
using EasyForNet.Modules;
using EasyForNet.Tests.Share;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EasyForNet.EntityFramework.Tests
{
    [DependOn(typeof(EasyForNetModule))]
    [DependOn(typeof(EasyForNetEntityFrameworkModule))]
    [DependOn(typeof(EasyForNetTestsShareModule))]
    public class EasyForNetEntityFrameworkTestsModule : ModuleBase
    {
        public override void Dependencies(IServiceCollection services, IConfiguration configuration)
        {
            services.AddScoped(sp =>
            {
                var options = new DbContextOptionsBuilder()
                    .UseSqlite("Data Source = EasyForNetEntityFrameworkTests.db")
                    .EnableSensitiveDataLogging()
                    .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
                    .Options;
                var currentUser = sp.GetRequiredService<ICurrentUser>();
                var db = new EasyForNetEntityFrameworkTestsDb(options, currentUser);
                return db;
            });
        }
    }
}