using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EasyForNet.EntityFramework.Configuration
{
    public abstract class EntityTypeConfiguration<TEntity> : IEntityTypeConfiguration<TEntity>
        where TEntity : class
    {
        private readonly string _tableName;

        protected EntityTypeConfiguration(string tableName)
        {
            _tableName = tableName;
        }

        public void Configure(EntityTypeBuilder<TEntity> builder)
        {
            builder.ToTable(_tableName);

            ConfigureEntity(builder);
        }

        protected abstract void ConfigureEntity(EntityTypeBuilder<TEntity> builder);
    }
}