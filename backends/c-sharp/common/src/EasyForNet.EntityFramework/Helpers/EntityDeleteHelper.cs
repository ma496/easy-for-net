using System;
using Ardalis.GuardClauses;
using EasyForNet.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EasyForNet.EntityFramework.Helpers
{
    public static class EntityDeleteHelper
    {
        public static void Delete<TDbContext, TEntity, TKey>(TDbContext dbContext, TEntity entity)
            where TDbContext : DbContext
            where TEntity : class, IEntity<TKey>
            where TKey : IComparable
        {
            Guard.Against.Null(dbContext, nameof(dbContext));
            Guard.Against.Null(entity, nameof(entity));

            var collection = dbContext.Set<TEntity>();
            if (entity is ISoftDeleteEntity softDeleteEntity)
            {
                softDeleteEntity.IsDeleted = true;
                collection.Update(entity);
            }
            else
            {
                collection.Remove(entity);
            }
        }
    }
}