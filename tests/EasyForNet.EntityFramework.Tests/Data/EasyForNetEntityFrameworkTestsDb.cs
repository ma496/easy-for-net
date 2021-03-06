using System.Diagnostics.CodeAnalysis;
using EasyForNet.EntityFramework.Data.Context;
using EasyForNet.EntityFramework.Tests.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace EasyForNet.EntityFramework.Tests.Data
{
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
    public class EasyForNetEntityFrameworkTestsDb : DbContextBase, ISettingDbContext
    {
        public EasyForNetEntityFrameworkTestsDb(DbContextOptions dbContextOptions, ICurrentUser currentUser) : base(
            dbContextOptions, currentUser)
        {
        }

        public DbSet<CustomerEntity> Customers { get; set; }

        public DbSet<TagEntity> Tags { get; set; }

        public DbSet<ProductEntity> Products { get; set; }

        public DbSet<ProductItemEntity> ProductItems { get; set; }

        public DbSet<SpecificHolidayEntity> SpecificHolidays { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            ISettingDbContext setting = this;
            setting.ConfigureSetting(modelBuilder);

            modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);
        }
    }
}