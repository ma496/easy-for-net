using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Linq.Expressions;
using System.Threading.Tasks;
using EasyForNet.Domain.Entities;
using EasyForNet.Exceptions.UserFriendly;
using EasyForNet.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.DynamicLinq;

namespace EasyForNet.EntityFramework.Helpers
{
    public static class EntityCreateHelper
    {
        public static async Task AddAsync<TDbContext, TEntity, TKey>(TDbContext dbContext, TEntity entity
            , params Expression<Func<TEntity, object>>[] propertyExps)
            where TDbContext : DbContext
            where TEntity : class, IEntity<TKey>
            where TKey : IComparable
        {
            var ups = propertyExps.Select(pe => new UniqueProperty(ObjectHelper.PropertyInfo(pe).Name))
                .ToList();

            await AddAsync<TDbContext, TEntity, TKey>(dbContext, entity, ups);
        }

        public static async Task AddAsync<TDbContext, TEntity, TKey>(TDbContext dbContext, TEntity entity,
            params (Expression<Func<TEntity, object>> propertyExp, bool isAllowDefaultValue)[] uniqueProperties)
            where TDbContext : DbContext
            where TEntity : class, IEntity<TKey>
            where TKey : IComparable
        {
            var ups = uniqueProperties.Select(up =>
                    new UniqueProperty(ObjectHelper.PropertyInfo(up.propertyExp).Name, up.isAllowDefaultValue))
                .ToList();

            await AddAsync<TDbContext, TEntity, TKey>(dbContext, entity, ups);
        }

        public static async Task AddAsync<TDbContext, TEntity, TKey>(TDbContext dbContext, TEntity entity
            , List<UniqueProperty> uniqueProperties)
            where TDbContext : DbContext
            where TEntity : class, IEntity<TKey>
            where TKey : IComparable
        {
            var collection = dbContext.Set<TEntity>();

            await CheckUniqueProperties<TEntity, TKey>(collection, entity, uniqueProperties);

            await collection.AddAsync(entity);
        }

        private static async Task CheckUniqueProperties<TEntity, TKey>(DbSet<TEntity> collection, TEntity entity,
            List<UniqueProperty> uniqueProperties)
            where TEntity : class, IEntity<TKey>
            where TKey : IComparable
        {
            foreach (var up in uniqueProperties) await CheckUniqueProperty<TEntity, TKey>(collection, entity, up);
        }

        private static async Task CheckUniqueProperty<TEntity, TKey>(DbSet<TEntity> collection, TEntity entity,
            UniqueProperty up)
            where TEntity : class, IEntity<TKey>
            where TKey : IComparable
        {
            var value = EntityHelper.PropertyValue(entity, up.Name, up.IsAllowDefaultValue);

            // value = value is string || value is DateTime ? $"\"{value}\"" : value;

            if (entity is ISoftDeleteEntity)
            {
                var e = await collection.IgnoreQueryFilters()
                    .Where($"{up.Name} = @0", value)
                    .Select($"new({nameof(ISoftDeleteEntity.IsDeleted)})")
                    .SingleOrDefaultAsync();

                if (e == null)
                    return;

                if (e.IsDeleted)
                    throw new UniquePropertyDeletedException(up.Name);
                throw new UniquePropertyException(up.Name);
            }

            if (await collection.IgnoreQueryFilters().Where($"{up.Name} = @0", value).AnyAsync())
            {
                throw new UniquePropertyException(up.Name);
            }
        }
    }
}