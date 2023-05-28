using EasyForNet.EntityFramework.Tests.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EasyForNet.EntityFramework.Tests.Data.Configuration;

public class SpecificHolidayEntityTypeConfiguration : IEntityTypeConfiguration<SpecificHolidayEntity>
{
    public void Configure(EntityTypeBuilder<SpecificHolidayEntity> builder)
    {
        builder.ToTable("SpecificHolidays");

        builder.HasIndex(e => e.Date)
            .IsUnique();
    }
}