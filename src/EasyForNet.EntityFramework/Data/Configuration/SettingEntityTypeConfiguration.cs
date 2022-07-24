using EasyForNet.EntityFramework.Configuration;
using EasyForNet.EntityFramework.Data.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EasyForNet.EntityFramework.Data.Configuration
{
    public class SettingEntityTypeConfiguration : EntityTypeConfiguration<EfnSettingEntity>
    {
        public SettingEntityTypeConfiguration(string tableName) : base(tableName)
        {
        }

        protected override void ConfigureEntity(EntityTypeBuilder<EfnSettingEntity> builder)
        {
            builder.HasIndex(e => e.Key)
                .IsUnique();
        }
    }
}
