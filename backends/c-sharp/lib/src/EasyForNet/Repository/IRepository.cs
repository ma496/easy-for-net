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
        TEntity Find(TKey key, bool isTracking = false);

        Task<TEntity> FindAsync(TKey key, bool isTracking = false);

        TEntity GetById(TKey key, bool isTracking = false);

        Task<TEntity> GetByIdAsync(TKey key, bool isTracking = false);

        void Delete(TKey key, bool isAutoSave = false);

        Task DeleteAsync(TKey key, bool isAutoSave = false);

        void DeleteRange(IEnumerable<TKey> keys, bool isAutoSave = false);

        Task DeleteRangeAsync(IEnumerable<TKey> keys, bool isAutoSave = false);
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
