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

namespace EasyForNet.EntityFramework.Crud;

public abstract class CrudAppService<TEntity, TKey, TGetListInput, TGetListDto, TCreateDto, TUpdateDto, TGetDto>
        : AppService, ICrudAppService<TKey, TGetListInput, TGetListDto, TCreateDto, TUpdateDto, TGetDto>
        where TEntity : class, IEntity<TKey>, new()
{
    private readonly IRepository<TEntity, TKey> _repository;

    protected CrudAppService(IRepository<TEntity, TKey> repository)
    {
        _repository = repository;
    }

    public async Task<PagedResultDto<TGetListDto>> GetListAsync(TGetListInput input)
    {
        var query = await ListQueryAsync(input) ?? await DefaultQueryAsync();
        query = ApplyFilter(query, input);
        var totalCount = query.Count();
        query = ApplySorting(query, input);
        query = ApplyPaging(query, input);
        var items = await query.ProjectTo<TGetListDto>(Mapper.ConfigurationProvider).ToListAsync();
        return new PagedResultDto<TGetListDto>(items, totalCount);
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
        var query = await GetQueryAsync(id) ?? await DefaultQueryAsync();
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

    protected virtual Task<IQueryable<TEntity>> DefaultQueryAsync()
    {
        return Task.FromResult(_repository.GetAll());
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

    #endregion
}
