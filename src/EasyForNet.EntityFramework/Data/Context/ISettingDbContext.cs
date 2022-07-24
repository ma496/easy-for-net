using EasyForNet.EntityFramework.Data.Configuration;
using Microsoft.EntityFrameworkCore;

namespace EasyForNet.EntityFramework.Data.Context
{
    public interface ISettingDbContext
    {
        public void ConfigureSetting(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfiguration(new SettingEntityTypeConfiguration("Settings"));
        }
    }
}
