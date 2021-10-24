using System.Diagnostics.CodeAnalysis;
using EasyForNet.EntityFramework.Data;
using EasyForNet.EntityFramework.Tests.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace EasyForNet.EntityFramework.Tests.Data
{
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
    public class EasyForNetEntityFrameworkTestsDb : DbContextBase
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
            modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);
        }
    }
}