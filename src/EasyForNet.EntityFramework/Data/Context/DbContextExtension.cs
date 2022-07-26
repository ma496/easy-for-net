using EasyForNet.EntityFramework.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace EasyForNet.EntityFramework.Data.Context
{
    public static class DbContextExtension
    {
        public static void ConfigureSetting<TEntity>(this ModelBuilder modelBuilder, string tableName = "EfnSettings")
            where TEntity : EfnSettingEntity
        {
            modelBuilder.Entity<TEntity>()
                .ToTable(tableName)
                .HasIndex(e => e.Key)
                .IsUnique();
        }
    }
}
