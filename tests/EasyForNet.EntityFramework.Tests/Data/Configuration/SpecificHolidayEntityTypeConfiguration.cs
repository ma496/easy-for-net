using EasyForNet.EntityFramework.Configuration;
using EasyForNet.EntityFramework.Tests.Data.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EasyForNet.EntityFramework.Tests.Data.Configuration
{
    public class SpecificHolidayEntityTypeConfiguration : EntityTypeConfiguration<SpecificHolidayEntity>
    {
        public SpecificHolidayEntityTypeConfiguration() : base("SpecificHolidays")
        {
        }

        protected override void ConfigureEntity(EntityTypeBuilder<SpecificHolidayEntity> builder)
        {
            builder.HasIndex(e => e.Date)
                .IsUnique();
        }
    }
}