﻿using EasyForNet.Domain.Entities;
using EasyForNet.EntityFramework.Data.Context;
using EasyForNet.Repository;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Linq.Dynamic.Core;
using EasyForNet.Exceptions.UserFriendly;
using EasyForNet.EntityFramework.Helpers;

namespace EasyForNet.EntityFramework.Repository
{
    public class EfRepository<TDbContext, TEntity, TKey> : EfRepository<TDbContext, TEntity>, IRepository<TEntity, TKey>
        where TDbContext : DbContextBase
        where TEntity : class, IEntity<TKey>, new()
        where TKey : IComparable
    {
        private readonly TDbContext _dbContext;

        public EfRepository(TDbContext dbContext) : base(dbContext)
        {
            _dbContext = dbContext;
        }

        public TEntity Find(TKey key, bool isTracking = false)
        {
            return GetQuery(isTracking).FirstOrDefault(IdCompareExpression(key));
        }

        public async Task<TEntity> FindAsync(TKey key, bool isTracking = false)
        {
            return await GetQuery(isTracking).FirstOrDefaultAsync(IdCompareExpression(key));
        }

        public TEntity GetById(TKey key, bool isTracking = false)
        {
            var entity = Find(key, isTracking);
            if (entity == null)
                throw new EntityNotFoundException(EntityHelper.EntityName<TEntity>(), key);
            return entity;
        }

        public async Task<TEntity> GetByIdAsync(TKey key, bool isTracking = false)
        {
            var entity = await FindAsync(key, isTracking);
            if (entity == null)
                throw new EntityNotFoundException(EntityHelper.EntityName<TEntity>(), key);
            return entity;
        }

        public void Delete(TKey key, bool isAutoSave = false)
        {
            var entity = new TEntity { Id = key };
            _dbContext.Set<TEntity>().Remove(entity);
            if (isAutoSave)
                SaveChanges();
        }

        public async Task DeleteAsync(TKey key, bool isAutoSave = false)
        {
            var entity = new TEntity { Id = key };
            _dbContext.Set<TEntity>().Remove(entity);
            if (isAutoSave)
                await SaveChangesAsync();
        }

        public void DeleteRange(IEnumerable<TKey> keys, bool isAutoSave = false)
        {
            var entities = keys.Select(k => new TEntity { Id = k });
            _dbContext.Set<TEntity>().RemoveRange(entities);
            if (isAutoSave)
                SaveChanges();
        }

        public async Task DeleteRangeAsync(IEnumerable<TKey> keys, bool isAutoSave = false)
        {
            var entities = keys.Select(k => new TEntity { Id = k });
            _dbContext.Set<TEntity>().RemoveRange(entities);
            if (isAutoSave)
                await SaveChangesAsync();
        }

        #region Helpers

        protected static Expression<Func<TEntity, bool>> IdCompareExpression(TKey id)
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

    public class EfRepository<TDbContext, TEntity> : IRepository<TEntity>
        where TDbContext : DbContextBase
        where TEntity : class
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

        public int SaveChanges()
        {
            return _dbContext.SaveChanges();
        }

        public async Task<int> SaveChangesAsync()
        {
            return await _dbContext.SaveChangesAsync();
        }

        #region Helpers

        protected IQueryable<TEntity> GetQuery(bool isTracking)
        {
            var query = _dbContext.Set<TEntity>();
            return isTracking ? query.AsTracking() : query.AsNoTracking();
        }

        #endregion
    }
}