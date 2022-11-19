using EasyForNet.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EasyForNet.Repository
{
    public interface IRepository<TEntity, TKey> : IRepository<TEntity>
        where TEntity : class, IEntity<TKey>
        where TKey : IComparable
    {
        TEntity GetById(TKey id, bool isTracking = false);

        Task<TEntity> GetByIdAsync(TKey id, bool isTracking = false);

        void Delete(TKey id, bool isAutoSave = false);

        Task DeleteAsync(TKey id, bool isAutoSave = false);
    }

    public interface IRepository<TEntity>
        where TEntity : class
    {
        IQueryable<TEntity> GetAll(bool isTracking = false);

        void Create(TEntity entity, bool isAutoSave = false);

        Task CreateAsync(TEntity entity, bool isAutoSave = false);

        void CreateRange(List<TEntity> entities, bool isAutoSave = false);

        Task CreateRangeAsync(List<TEntity> entities, bool isAutoSave = false);

        void Update(TEntity entity, bool isAutoSave = false);

        Task UpdateAsync(TEntity entity, bool isAutoSave = false);

        void UpdateRange(List<TEntity> entities, bool isAutoSave = false);

        Task UpdateRangeAsync(List<TEntity> entities, bool isAutoSave = false);

        int SaveChanges();

        Task<int> SaveChangesAsync();
    }
}
