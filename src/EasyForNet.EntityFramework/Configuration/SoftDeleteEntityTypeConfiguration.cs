using EasyForNet.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EasyForNet.EntityFramework.Configuration
{
    public abstract class SoftDeleteEntityTypeConfiguration<TEntity> : IEntityTypeConfiguration<TEntity>
        where TEntity : class, ISoftDeleteEntity
    {
        private readonly string _tableName;

        protected SoftDeleteEntityTypeConfiguration(string tableName)
        {
            _tableName = tableName;
        }
        
        public void Configure(EntityTypeBuilder<TEntity> builder)
        {
            builder.ToTable(_tableName);

            builder.Property(e => e.IsDeleted)
                .HasDefaultValue(false);

            builder.HasQueryFilter(e => e.IsDeleted == false);

            ConfigureEntity(builder);
        }

        protected abstract void ConfigureEntity(EntityTypeBuilder<TEntity> builder);
    }
}