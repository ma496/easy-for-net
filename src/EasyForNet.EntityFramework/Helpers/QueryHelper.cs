using System;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using EasyForNet.Domain.Entities;
using EasyForNet.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace EasyForNet.EntityFramework.Helpers
{
    public static class QueryHelper
    {
        public static async Task<TEntity> GetEntityAsync<TEntity, TKey>(DbSet<TEntity> collection, TKey id)
            where TEntity : class, IEntity<TKey>
            where TKey : IComparable
        {
            var entity = await collection.FindAsync(id);

            if (entity == null)
                throw new NoContentException($"No {EntityHelper.EntityName(typeof(TEntity))} found with id = {id}");

            return entity;
        }

        public static async Task<TDto> GetAsync<TEntity, TKey, TDto>(DbSet<TEntity> collection, long id,
            IConfigurationProvider configurationProvider)
            where TEntity : class, IEntity<TKey>
            where TKey : IComparable
        {
            var obj = await collection
                .AsNoTracking()
                .Where($"{nameof(IEntity<TKey>.Id)} = @0", id)
                .ProjectTo<TDto>(configurationProvider)
                .SingleOrDefaultAsync();

            if (obj == null)
                throw new NoContentException($"No {EntityHelper.EntityName(typeof(TEntity))} found with id = {id}");

            return obj;
        }

        public static async Task ExistAndThrowAsync<TEntity, TKey>(DbSet<TEntity> collection, TKey id)
            where TEntity : class, IEntity<TKey>
            where TKey : IComparable
        {
            var entity = await collection
                .Where($"{nameof(IEntity<TKey>.Id)} = @0", id)
                .Select(e => new {e.Id})
                .SingleOrDefaultAsync();

            if (entity == null)
                throw new NoContentException($"No {EntityHelper.EntityName(typeof(TEntity))} found with id = {id}");
        }
    }
}