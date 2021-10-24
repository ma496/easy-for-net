using EasyForNet.EntityFramework.Configuration;
using EasyForNet.EntityFramework.Tests.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EasyForNet.EntityFramework.Tests.Data.Configuration
{
    public class ProductEntityTypeConfiguration : EntityTypeConfiguration<ProductEntity>
    {
        public ProductEntityTypeConfiguration() : base("Products")
        {
        }
        
        protected override void ConfigureEntity(EntityTypeBuilder<ProductEntity> builder)
        {
            builder.HasIndex(p => p.Model)
                .IsUnique();

            builder.HasMany(p => p.Items)
                .WithOne(i => i.Product)
                .HasForeignKey(i => i.ProductId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}