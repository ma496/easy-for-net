using System.Collections.Generic;
using System.Threading.Tasks;
using EasyForNet.Bogus;
using Microsoft.EntityFrameworkCore;

namespace EasyForNet.EntityFramework.Bogus
{
    public abstract class DataGenerator<TDbContext, TEntity> : DataGenerator<TEntity>
        where TDbContext : DbContext
        where TEntity : class
    {
        private readonly DbSet<TEntity> _collection;
        private readonly TDbContext _dbContext;

        protected DataGenerator(TDbContext dbContext)
        {
            _dbContext = dbContext;
            _collection = dbContext.Set<TEntity>();
        }

        public async Task<TEntity> GenerateAndSaveAsync()
        {
            var entity = Faker().Generate();

            await _collection.AddAsync(entity);
            await _dbContext.SaveChangesAsync();

            return entity;
        }

        public async Task<List<TEntity>> GenerateAndSaveAsync(int count)
        {
            var entities = Faker().Generate(count);

            await _collection.AddRangeAsync(entities);
            await _dbContext.SaveChangesAsync();

            return entities;
        }
    }
}