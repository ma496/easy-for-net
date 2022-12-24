using EasyForNet.EntityFramework.Tests.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EasyForNet.EntityFramework.Tests.Data.Configuration;

public class CustomerEntityTypeConfiguration : IEntityTypeConfiguration<CustomerEntity>
{
    public void Configure(EntityTypeBuilder<CustomerEntity> builder)
    {
        builder.ToTable("Customers");

        builder.HasIndex(c => c.Code)
            .IsUnique();

        builder.HasIndex(c => c.IdCard)
            .IsUnique();
    }
}