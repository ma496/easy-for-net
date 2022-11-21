using EasyForNet.EntityFramework.Configuration;
using EasyForNet.EntityFramework.Tests.Data.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EasyForNet.EntityFramework.Tests.Data.Configuration;

public class CustomerEntityTypeConfiguration : SoftDeleteEntityTypeConfiguration<CustomerEntity>
{
    public CustomerEntityTypeConfiguration() : base("Customers")
    {
    }

    protected override void ConfigureEntity(EntityTypeBuilder<CustomerEntity> builder)
    {
        builder.HasIndex(c => c.Code)
            .IsUnique();

        builder.HasIndex(c => c.IdCard)
            .IsUnique();
    }
}