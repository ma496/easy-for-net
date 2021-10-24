using EasyForNet.EntityFramework.Configuration;
using EasyForNet.EntityFramework.Tests.Data.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EasyForNet.EntityFramework.Tests.Data.Configuration
{
    public class TagEntityTypeConfiguration : EntityTypeConfiguration<TagEntity>
    {
        public TagEntityTypeConfiguration() : base("Tags")
        {
        }

        protected override void ConfigureEntity(EntityTypeBuilder<TagEntity> builder)
        {
            builder.HasIndex(t => t.Name)
                .IsUnique();
        }
    }
}