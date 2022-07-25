using EasyForNet.EntityFramework.Data.Configuration;
using Microsoft.EntityFrameworkCore;

namespace EasyForNet.EntityFramework.Data.Context
{
    public static class DbContextExtension
    {
        public static void ConfigureSetting(this ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfiguration(new SettingEntityTypeConfiguration("Settings"));
        }
    }
}
