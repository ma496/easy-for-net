using EasyForNet.Domain.Entities;
using EasyForNet.EntityFramework.Data.Context;
using EasyForNet.Repository;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Linq.Dynamic.Core;

namespace EasyForNet.EntityFramework.Repository
{
    public class EfRepository<TDbContext, TEntity, TKey> : IRepository<TEntity, TKey>
        where TDbContext : DbContextBase
        where TEntity : class, IEntity<TKey>
        where TKey : IComparable
    {
        private readonly TDbContext _dbContext;

        public EfRepository(TDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public IQueryable<TEntity> GetAll(bool isTracking = false)
        {
            return GetQuery(isTracking);
        }

        public TEntity GetById(TKey id, bool isTracking = false)
        {
            return GetQuery(isTracking).FirstOrDefault(IdCompareExpression(id));
        }

        public async Task<TEntity> GetByIdAsync(TKey id, bool isTracking = false)
        {
            return await GetQuery(isTracking).FirstOrDefaultAsync(IdCompareExpression(id));
        }

        public void Create(TEntity entity, bool isAutoSave = false)
        {
            _dbContext.Set<TEntity>().Add(entity);
            if (isAutoSave) 
                SaveChanges();
        }

        public async Task CreateAsync(TEntity entity, bool isAutoSave = false)
        {
            await _dbContext.Set<TEntity>().AddAsync(entity);
            if (isAutoSave)
                await SaveChangesAsync();
        }

        public void CreateRange(List<TEntity> entities, bool isAutoSave = false)
        {
            _dbContext.Set<TEntity>().AddRange(entities);
            if (isAutoSave) SaveChanges();
        }

        public async Task CreateRangeAsync(List<TEntity> entities, bool isAutoSave = false)
        {
            await _dbContext.Set<TEntity>().AddRangeAsync(entities);
            if (isAutoSave) await SaveChangesAsync();
        }

        public void Update(TEntity entity, bool isAutoSave = false)
        {
            _dbContext.Set<TEntity>().Update(entity);
            if (isAutoSave)
                SaveChanges();
        }

        public async Task UpdateAsync(TEntity entity, bool isAutoSave = false)
        {
            _dbContext.Set<TEntity>().Update(entity);
            if (isAutoSave)
                await SaveChangesAsync();
        }

        public void UpdateRange(List<TEntity> entities, bool isAutoSave = false)
        {
            _dbContext.Set<TEntity>().UpdateRange(entities);
            if (isAutoSave) SaveChanges();
        }

        public async Task UpdateRangeAsync(List<TEntity> entities, bool isAutoSave = false)
        {
            _dbContext.Set<TEntity>().UpdateRange(entities);
            if (isAutoSave) await SaveChangesAsync();
        }

        public void Delete(TKey id, bool isAutoSave = false)
        {
            var entity = GetById(id);
            _dbContext.Set<TEntity>().Remove(entity);
            if (isAutoSave)
                SaveChanges();
        }

        public async Task DeleteAsync(TKey id, bool isAutoSave = false)
        {
            var entity = await GetByIdAsync(id);
            _dbContext.Set<TEntity>().Remove(entity);
            if (isAutoSave)
                await SaveChangesAsync();
        }

        public int SaveChanges()
        {
            return _dbContext.SaveChanges();
        }

        public async Task<int> SaveChangesAsync()
        {
            return await _dbContext.SaveChangesAsync();
        }

        #region Helpers

        private IQueryable<TEntity> GetQuery(bool isTracking)
        {
            var query = _dbContext.Set<TEntity>();
            return isTracking ? query.AsTracking() : query.AsNoTracking();
        }

        private static Expression<Func<TEntity, bool>> IdCompareExpression(TKey id)
        {
            var primaryKeyName = nameof(IEntity<TKey>.Id);
            ParameterExpression pe = Expression.Parameter(typeof(TEntity), "entity");
            MemberExpression me = Expression.Property(pe, primaryKeyName);
            ConstantExpression constant = Expression.Constant(id, id.GetType());
            BinaryExpression body = Expression.Equal(me, constant);
            Expression<Func<TEntity, bool>> expressionTree = Expression.Lambda<Func<TEntity, bool>>(body, new[] { pe });
            return expressionTree;
        }

        #endregion
    }
}
