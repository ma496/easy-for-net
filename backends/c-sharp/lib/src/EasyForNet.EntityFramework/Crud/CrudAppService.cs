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

namespace EasyForNet.EntityFramework.Crud;

public abstract class CrudAppService<TEntity, TKey, TListInput, TListDto, TCreateDto, TUpdateDto, TGetDto>
        : AppService, ICrudAppService<TKey, TListInput, TListDto, TCreateDto, TUpdateDto, TGetDto>
        where TEntity : class, IEntity<TKey>, new()
{
    private readonly IRepository<TEntity, TKey> _repository;

    protected CrudAppService(IRepository<TEntity, TKey> repository)
    {
        _repository = repository;
    }

    public IQueryable<TListDto> List(TListInput input)
    {
        var query = ListQuery(input) ?? DefaultQuery();
        query = ApplyFilter(query, input);
        query = ApplySorting(query, input);
        query = ApplyPaging(query, input);
        return query.ProjectTo<TListDto>(Mapper.ConfigurationProvider);
    }

    public async Task<TGetDto> CreateAsync(TCreateDto input)
    {
        var entity = Mapper.Map<TEntity>(input);
        await _repository.CreateAsync(entity, true);
        var dto = Mapper.Map<TGetDto>(entity);
        return dto;
    }

    public async Task<TGetDto> UpdateAsync(TKey id, TUpdateDto input)
    {
        var entity = await _repository.GetByIdAsync(id);
        Mapper.Map(input, entity);
        await _repository.UpdateAsync(entity, true);
        var dto = Mapper.Map<TGetDto>(entity);
        return dto;
    }

    public async Task<TGetDto> GetAsync(TKey id)
    {
        var query = GetQuery(id) ?? DefaultQuery();
        return await query
            .Where(e => e.Id.Equals(id))
            .ProjectTo<TGetDto>(Mapper.ConfigurationProvider)
            .SingleOrDefaultAsync();
    }

    public async Task DeleteAsync(TKey id)
    {
        await _repository.DeleteAsync(id);
    }

    public async Task UndoDeleteAsync(TKey id)
    {
        await _repository.UndoDeleteAsync(id);
    }

    #region Helpers

    protected virtual IQueryable<TEntity> DefaultQuery()
    {
        return _repository.GetAll();
    }

    protected virtual IQueryable<TEntity> ListQuery(TListInput input)
    {
        return null;
    }

    protected virtual IQueryable<TEntity> GetQuery(TKey id)
    {
        return null;
    }

    protected virtual IQueryable<TEntity> ApplyFilter(IQueryable<TEntity> query, TListInput input)
    {
        return query;
    }

    protected virtual IQueryable<TEntity> ApplySorting(IQueryable<TEntity> query, TListInput input)
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

    protected virtual IQueryable<TEntity> ApplyPaging(IQueryable<TEntity> query, TListInput input)
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

    #endregion
}
