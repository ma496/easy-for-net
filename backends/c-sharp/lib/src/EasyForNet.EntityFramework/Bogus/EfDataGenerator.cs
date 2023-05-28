using System.Collections.Generic;
using System.Threading.Tasks;
using EasyForNet.Bogus;
using EasyForNet.Repository;

namespace EasyForNet.EntityFramework.Bogus;

public abstract class EfDataGenerator<TEntity> : DataGenerator<TEntity>
    where TEntity : class
{
    public IRepository<TEntity> Repository { get; set; }

    public async Task<TEntity> GenerateAndSaveAsync()
    {
        var entity = Faker().Generate();

        await Repository.CreateAsync(entity, true);

        return entity;
    }

    public async Task<List<TEntity>> GenerateAndSaveAsync(int count)
    {
        var entities = Faker().Generate(count);

        await Repository.CreateRangeAsync(entities, true);

        return entities;
    }
}