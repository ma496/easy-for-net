using EasyForNet.EntityFramework.Tests.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EasyForNet.EntityFramework.Tests.Data.Configuration;

public class ProductItemEntityTypeConfiguration : IEntityTypeConfiguration<ProductItemEntity>
{
    public void Configure(EntityTypeBuilder<ProductItemEntity> builder)
    {
        builder.ToTable("ProductItems");

        builder.HasIndex(pi => pi.SerialNo)
            .IsUnique();
    }
}