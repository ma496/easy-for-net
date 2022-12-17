using EasyForNet.Domain.Entities;
using EasyForNet.Repository;
using System.Linq;
using System.Threading.Tasks;
using System;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;
using EasyForNet.Application.Services;
using EasyForNet.Application.Services.Crud;
using System.Linq.Dynamic.Core;
using Autofac;
using EasyForNet.Domain.Entities.Audit;
using EasyForNet.Application.Dto.Crud;
using System.Linq.Expressions;
using EasyForNet.Exceptions.UserFriendly;
using Ardalis.GuardClauses;

namespace EasyForNet.EntityFramework.Crud;

public abstract class CrudAppService<TEntity, TKey, TGetListInput, TDto>
    : CrudAppService<TEntity, TKey, TGetListInput, TDto, TDto, TDto, TDto>
    where TEntity : class, IEntity<TKey>
{
    protected CrudAppService(IRepository<TEntity, TKey> repository) : base(repository)
    {
    }
}

public abstract class CrudAppService<TEntity, TKey, TGetListInput, TDto, TCreateOrUpdateDto>
    : CrudAppService<TEntity, TKey, TGetListInput, TDto, TCreateOrUpdateDto, TCreateOrUpdateDto, TDto>
    where TEntity : class, IEntity<TKey>
{
    protected CrudAppService(IRepository<TEntity, TKey> repository) : base(repository)
    {
    }
}

public abstract class CrudAppService<TEntity, TKey, TGetListInput, TDto, TCreateDto, TUpdateDto>
    : CrudAppService<TEntity, TKey, TGetListInput, TDto, TCreateDto, TUpdateDto, TDto>
    where TEntity : class, IEntity<TKey>
{
    protected CrudAppService(IRepository<TEntity, TKey> repository) : base(repository)
    {
    }
}

public abstract class CrudAppService<TEntity, TKey, TGetListInput, TGetListDto, TCreateDto, TUpdateDto, TGetDto>
        : AppService, ICrudAppService<TKey, TGetListInput, TGetListDto, TCreateDto, TUpdateDto, TGetDto>
        where TEntity : class, IEntity<TKey>
{
    protected IRepository<TEntity, TKey> Repository { get; }

    protected CrudAppService(IRepository<TEntity, TKey> repository)
    {
        Repository = repository;
    }

    public virtual async Task<PagedResultDto<TGetListDto>> GetListAsync(TGetListInput input)
    {
        var query = await (ListQueryAsync(input) ?? DefaultQueryAsync());
        query = ApplyFilter(query, input);
        var totalCount = query.Count();
        query = ApplySorting(query, input);
        query = ApplyPaging(query, input);
        var items = await query.ProjectTo<TGetListDto>(Mapper.ConfigurationProvider).ToListAsync();
        return new PagedResultDto<TGetListDto>(items, totalCount);
    }

    public virtual async Task<TGetDto> CreateAsync(TCreateDto input)
    {
        var entity = Mapper.Map<TEntity>(input);
        await Repository.CreateAsync(entity, true);
        var dto = Mapper.Map<TGetDto>(entity);
        return dto;
    }

    public virtual async Task<TGetDto> UpdateAsync(TKey id, TUpdateDto input)
    {
        var entity = await Repository.GetByIdAsync(id);
        Mapper.Map(input, entity);
        await Repository.UpdateAsync(entity, true);
        var dto = Mapper.Map<TGetDto>(entity);
        return dto;
    }

    public virtual async Task<TGetDto> GetAsync(TKey id)
    {
        var query = await (GetQueryAsync(id) ?? DefaultQueryAsync());
        return await query
            .Where(e => e.Id.Equals(id))
            .ProjectTo<TGetDto>(Mapper.ConfigurationProvider)
            .SingleOrDefaultAsync();
    }

    public virtual async Task DeleteAsync(TKey id)
    {
        await Repository.DeleteAsync(id, true);
    }

    public virtual async Task UndoDeleteAsync(TKey id)
    {
        await Repository.UndoDeleteAsync(id, true);
    }

    #region Helpers

    protected virtual Task<IQueryable<TEntity>> DefaultQueryAsync()
    {
        return Task.FromResult(Repository.GetAll());
    }

    protected virtual Task<IQueryable<TEntity>> ListQueryAsync(TGetListInput input)
    {
        return null;
    }

    protected virtual Task<IQueryable<TEntity>> GetQueryAsync(TKey id)
    {
        return null;
    }

    protected virtual IQueryable<TEntity> ApplyFilter(IQueryable<TEntity> query, TGetListInput input)
    {
        return query;
    }

    protected virtual IQueryable<TEntity> ApplySorting(IQueryable<TEntity> query, TGetListInput input)
    {
        if (input is ISortedResultRequest sortInput)
        {
            if (!sortInput.Sorting.IsNullOrWhiteSpace())
            {
                return query.OrderBy(sortInput.Sorting);
            }
        }

        if (input is ILimitedResultRequest)
        {
            return ApplyDefaultSorting(query);
        }

        return query;
    }

    protected virtual IQueryable<TEntity> ApplyPaging(IQueryable<TEntity> query, TGetListInput input)
    {
        if (input is IPagedResultRequest pagedInput)
        {
            return query.PageBy(pagedInput);
        }

        if (input is ILimitedResultRequest limitedInput)
        {
            return query.Take(limitedInput.MaxResultCount);
        }

        return query;
    }

    protected virtual IQueryable<TEntity> ApplyDefaultSorting(IQueryable<TEntity> query)
    {
        if (typeof(TEntity).IsAssignableTo<IAuditEntity>())
        {
            return query.OrderByDescending(e => ((IAuditEntity)e).CreatedAt);
        }

        throw new Exception("No sorting specified but this query requires sorting. Override the ApplyDefaultSorting method for your application service derived from CrudAppService!");
    }

    protected virtual async Task AnyAsync(Expression<Func<TEntity, bool>> predicate, string propertyName,
        TKey id = default, string errorMessage = null, bool isUserFriendlyException = true)
    {
        Guard.Against.Null(predicate, nameof(predicate));
        Guard.Against.NullOrWhiteSpace(propertyName, nameof(propertyName));

        var query = Repository.GetAll().Where(predicate);
        if (!id.Equals(default(TKey)))
        {
            query = query.Where(e => !e.Id.Equals(id));
        }
        var isAny = await query.AnyAsync();
        if (isAny)
        {
            if (isUserFriendlyException)
                throw new DuplicateException(propertyName, errorMessage);
            else
                throw new Exception(errorMessage.IsNullOrWhiteSpace() 
                    ? $"Value of {propertyName} already exist"
                    : errorMessage);
        }
    }

    #endregion
}
