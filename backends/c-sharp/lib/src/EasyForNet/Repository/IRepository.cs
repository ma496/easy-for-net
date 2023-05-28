using EasyForNet.Domain.Entities;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EasyForNet.Repository;

public interface IRepository<TEntity, in TKey> : IRepository<TEntity>
    where TEntity : class, IEntity<TKey>
{
    TEntity Find(TKey id, bool isTracking = false);

    Task<TEntity> FindAsync(TKey id, bool isTracking = false);

    TEntity GetById(TKey id, bool isTracking = false);

    Task<TEntity> GetByIdAsync(TKey id, bool isTracking = false);

    void Delete(TKey id, bool isAutoSave = false);

    Task DeleteAsync(TKey id, bool isAutoSave = false);

    void DeleteRange(IEnumerable<TKey> ids, bool isAutoSave = false);

    Task DeleteRangeAsync(IEnumerable<TKey> ids, bool isAutoSave = false);

    void UndoDelete(TKey id, bool isAutoSave = false);

    Task UndoDeleteAsync(TKey id, bool isAutoSave = false);

    void UndoDeleteRange(IEnumerable<TKey> ids, bool isAutoSave = false);

    Task UndoDeleteRangeAsync(IEnumerable<TKey> ids, bool isAutoSave = false);
}

public interface IRepository<TEntity>
    where TEntity : class
{
    IQueryable<TEntity> GetAll(bool isTracking = false);

    void Create(TEntity entity, bool isAutoSave = false);

    Task CreateAsync(TEntity entity, bool isAutoSave = false);

    void CreateRange(IEnumerable<TEntity> entities, bool isAutoSave = false);

    Task CreateRangeAsync(IEnumerable<TEntity> entities, bool isAutoSave = false);

    void Update(TEntity entity, bool isAutoSave = false);

    Task UpdateAsync(TEntity entity, bool isAutoSave = false);

    void UpdateRange(IEnumerable<TEntity> entities, bool isAutoSave = false);

    Task UpdateRangeAsync(IEnumerable<TEntity> entities, bool isAutoSave = false);

    void Delete(TEntity entity, bool isAutoSave = false);

    Task DeleteAsync(TEntity entity, bool isAutoSave = false);

    void DeleteRange(IEnumerable<TEntity> entities, bool isAutoSave = false);

    Task DeleteRangeAsync(IEnumerable<TEntity> entities, bool isAutoSave = false);

    void UndoDelete(TEntity entity, bool isAutoSave = false);

    Task UndoDeleteAsync(TEntity entity, bool isAutoSave = false);

    void UndoDeleteRange(IEnumerable<TEntity> entities, bool isAutoSave = false);

    Task UndoDeleteRangeAsync(IEnumerable<TEntity> entities, bool isAutoSave = false);

    int SaveChanges();

    Task<int> SaveChangesAsync();
}