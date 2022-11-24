using EasyForNet.EntityFramework.Data.Context;
using EasyForNet.Repository;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using EasyForNet.Exceptions.UserFriendly;
using EasyForNet.EntityFramework.Helpers;
using EasyForNet.Domain.Entities;
using EasyForNet.Extensions;

namespace EasyForNet.EntityFramework.Repository;

public class EfRepository<TDbContext, TEntity, TKey> : EfRepository<TDbContext, TEntity>, IRepository<TEntity, TKey>
    where TDbContext : DbContextBase
    where TEntity : class, IEntity<TKey>
{
    private readonly TDbContext _dbContext;

    protected EfRepository(TDbContext dbContext) : base(dbContext)
    {
        _dbContext = dbContext;
    }

    public TEntity Find(TKey id, bool isTracking = false)
    {
        return GetQuery(isTracking).FirstOrDefault(e => e.Id.Equals(id));
    }

    public async Task<TEntity> FindAsync(TKey id, bool isTracking = false)
    {
        return await GetQuery(isTracking).FirstOrDefaultAsync(e => e.Id.Equals(id));
    }

    public TEntity GetById(TKey id, bool isTracking = false)
    {
        var entity = Find(id, isTracking);
        if (entity == null)
            throw new EntityNotFoundException(EntityHelper.EntityName<TEntity>(), id);
        return entity;
    }

    public async Task<TEntity> GetByIdAsync(TKey id, bool isTracking = false)
    {
        var entity = await FindAsync(id, isTracking);
        if (entity == null)
            throw new EntityNotFoundException(EntityHelper.EntityName<TEntity>(), id);
        return entity;
    }

    public void Delete(TKey id, bool isAutoSave = false)
    {
        var entity = GetById(id);

        Delete(entity, isAutoSave);
    }

    public async Task DeleteAsync(TKey id, bool isAutoSave = false)
    {
        var entity = await GetByIdAsync(id);

        await DeleteAsync(entity, isAutoSave);
    }

    public void Delete(TEntity entity, bool isAutoSave = false)
    {
        if (entity == null) return;
        
        if (entity is ISoftDeleteEntity softDeleteEntity)
        {
            softDeleteEntity.IsDeleted = true;
            _dbContext.Set<TEntity>().Update(entity);
        }
        else
        {
            _dbContext.Set<TEntity>().Remove(entity);
        }
        
        if (isAutoSave)
            SaveChanges();
    }
    
    public async Task DeleteAsync(TEntity entity, bool isAutoSave = false)
    {
        if (entity == null) return;
        
        if (entity is ISoftDeleteEntity softDeleteEntity)
        {
            softDeleteEntity.IsDeleted = true;
            _dbContext.Set<TEntity>().Update(entity);
        }
        else
        {
            _dbContext.Set<TEntity>().Remove(entity);
        }
        
        if (isAutoSave)
            await SaveChangesAsync();
    }

    public void DeleteRange(IEnumerable<TKey> ids, bool isAutoSave = false)
    {
        var entities = GetQuery(false).Where(e => ids.Contains(e.Id)).ToList();
        
        DeleteRange(entities, isAutoSave);
    }

    public async Task DeleteRangeAsync(IEnumerable<TKey> ids, bool isAutoSave = false)
    {
        var entities = await GetQuery(false).Where(e => ids.Contains(e.Id)).ToListAsync();

        await DeleteRangeAsync(entities, isAutoSave);
    }

    public void DeleteRange(IEnumerable<TEntity> entities, bool isAutoSave = false)
    {
        var array = entities as TEntity[] ?? entities.ToArray();
        if (array.IsNullOrEmpty()) return;
        
        if (typeof(ISoftDeleteEntity).IsAssignableFrom(typeof(TEntity)))
        {
            foreach (var entity in array)
            {
                var softDeleteEntity = (ISoftDeleteEntity)entity;
                softDeleteEntity.IsDeleted = true;
            }
            _dbContext.Set<TEntity>().UpdateRange(array);
        }
        else
        {
            _dbContext.Set<TEntity>().RemoveRange(array);
        }
        
        if (isAutoSave)
            SaveChanges();
    }
    
    public async Task DeleteRangeAsync(IEnumerable<TEntity> entities, bool isAutoSave = false)
    {
        var array = entities as TEntity[] ?? entities.ToArray();
        if (array.IsNullOrEmpty()) return;
        
        if (typeof(ISoftDeleteEntity).IsAssignableFrom(typeof(TEntity)))
        {
            foreach (var entity in array)
            {
                var softDeleteEntity = (ISoftDeleteEntity)entity;
                softDeleteEntity.IsDeleted = true;
            }
            _dbContext.Set<TEntity>().UpdateRange(array);
        }
        else
        {
            _dbContext.Set<TEntity>().RemoveRange(array);
        }
        
        if (isAutoSave)
            await SaveChangesAsync();
    }
}

public class EfRepository<TDbContext, TEntity> : IRepository<TEntity>
    where TDbContext : DbContextBase
    where TEntity : class
{
    private readonly TDbContext _dbContext;

    protected EfRepository(TDbContext dbContext)
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

    public void CreateRange(IEnumerable<TEntity> entities, bool isAutoSave = false)
    {
        _dbContext.Set<TEntity>().AddRange(entities);
        if (isAutoSave) SaveChanges();
    }

    public async Task CreateRangeAsync(IEnumerable<TEntity> entities, bool isAutoSave = false)
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

    public void UpdateRange(IEnumerable<TEntity> entities, bool isAutoSave = false)
    {
        _dbContext.Set<TEntity>().UpdateRange(entities);
        if (isAutoSave) SaveChanges();
    }

    public async Task UpdateRangeAsync(IEnumerable<TEntity> entities, bool isAutoSave = false)
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