using EasyForNet.EntityFramework.Configuration;
using EasyForNet.EntityFramework.Tests.Data.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EasyForNet.EntityFramework.Tests.Data.Configuration
{
    public class ProductItemEntityTypeConfiguration : EntityTypeConfiguration<ProductItemEntity>
    {
        public ProductItemEntityTypeConfiguration() : base("ProductItems")
        {
        }

        protected override void ConfigureEntity(EntityTypeBuilder<ProductItemEntity> builder)
        {
            builder.HasIndex(pi => pi.SerialNo)
                .IsUnique();
        }
    }
}