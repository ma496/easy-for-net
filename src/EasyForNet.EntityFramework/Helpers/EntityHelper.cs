using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;

namespace EasyForNet.EntityFramework.Helpers
{
    public static class EntityHelper
    {
        public static string EntityName(Type type)
        {
            return type.Name.Replace("Entity", "").ToLower();
        }

        public static async Task<object> PropertyValue(object obj, string propertyName, bool isAllowDefaultValue)
        {
            var property = obj.GetType().GetProperty(propertyName);
            if (property == null)
                throw new Exception($"{propertyName} not found on {obj.GetType().FullName}");

            var value = property.GetValue(obj);
            if (value == default && !isAllowDefaultValue)
            {
                throw new Exception($"Not allowed default value for {propertyName}: {property.PropertyType.FullName}");
            }

            if (property.PropertyType.IsEnum)
                return await Task.Run(() => Convert.ToInt32(value));
            return await Task.Run(() => value);
        }

        public static async Task RemoveRowsAsync<TEntity, T>(DbSet<TEntity> dbSet,
            [NotNull] List<TEntity> oldRows, List<T> newRows, [NotNull] Func<TEntity, T, bool> match,
            [NotNull] Func<T, TEntity> create)
            where TEntity : class
        {
            Guard.Against.Null(oldRows, nameof(oldRows));

            newRows ??= new List<T>();

            Guard.Against.Null(match, nameof(match));
            Guard.Against.Null(create, nameof(create));

            var removeEntities = new List<TEntity>();

            foreach (var or in oldRows)
                if (!newRows.Exists(nr => match(or, nr)))
                    removeEntities.Add(or);

            if (removeEntities.Count > 0)
                dbSet.RemoveRange(removeEntities);

            await Task.CompletedTask;
        }

        public static async Task AddRemoveRowsAsync<TEntity, T>(DbSet<TEntity> dbSet,
            [NotNull] List<TEntity> oldRows, List<T> newRows, [NotNull] Func<TEntity, T, bool> match,
            [NotNull] Func<T, TEntity> create)
            where TEntity : class
        {
            Guard.Against.Null(oldRows, nameof(oldRows));

            newRows ??= new List<T>();

            Guard.Against.Null(match, nameof(match));
            Guard.Against.Null(create, nameof(create));

            var newEntities = new List<TEntity>();
            var removeEntities = new List<TEntity>();
            foreach (var nr in newRows)
                if (!oldRows.Exists(or => match(or, nr)))
                    newEntities.Add(create(nr));

            foreach (var or in oldRows)
                if (!newRows.Exists(nr => match(or, nr)))
                    removeEntities.Add(or);

            if (newEntities.Count > 0)
                await dbSet.AddRangeAsync(newEntities);
            if (removeEntities.Count > 0)
                dbSet.RemoveRange(removeEntities);
        }
    }
}