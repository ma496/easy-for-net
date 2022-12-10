using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using EasyForNet.Application.Dto.Entities;
using EasyForNet.Application.Helpers;
using EasyForNet.Domain.Entities;
using EasyForNet.EntityFramework.Data.Context;
using EasyForNet.EntityFramework.Helpers;
using EasyForNet.Exceptions.UserFriendly;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace EasyForNet.EntityFramework.Crud;

public abstract class CrudActions<TDbContext, TEntity, TKey, TListDto, TCreateDto, TCreateResponseDto, TUpdateDto, TUpdateResponseDto, TGetDto>
    : ICrudActions<TKey, TListDto, TCreateDto, TCreateResponseDto, TUpdateDto, TUpdateResponseDto, TGetDto>
    where TDbContext : DbContextBase
    where TEntity : class, IEntity<TKey>, new()
    where TKey : IComparable
    where TListDto : class, IEntityDto<TKey>
    where TCreateDto : class
    where TCreateResponseDto: class, IEntityDto<TKey>, TCreateDto
    where TUpdateDto : class
    where TUpdateResponseDto : class, IEntityDto<TKey>, TUpdateDto
    where TGetDto : class, IEntityDto<TKey>
{
    protected TDbContext DbContext { get; }

    protected IMapper Mapper { get; }

    protected bool IsValidate { get; }

    protected bool IsValidateUsingAttributes { get; }

    protected DbSet<TEntity> Items { get; }

    protected CrudActions(TDbContext dbContext, IMapper mapper, bool isValidate = false,
        bool isValidateUsingAttributes = true)
    {
        DbContext = dbContext;
        Mapper = mapper;
        IsValidate = isValidate;
        IsValidateUsingAttributes = isValidateUsingAttributes;
        Items = DbContext.Set<TEntity>();
    }

    public async Task<IQueryable<TListDto>> ListAsync()
    {
        return (await BeforeListAsync(Items))
            .ProjectTo<TListDto>(Mapper.ConfigurationProvider);
    }

    public async Task<TCreateResponseDto> CreateAsync(TCreateDto dto)
    {
        var entity = Mapper.Map<TEntity>(dto);

        if (IsValidate)
        {
            if (IsValidateUsingAttributes)
                ValidatorHelper.Validate(dto);
            if (CreateDtoValidator() != null)
                await ValidatorHelper.ValidateAsync(dto, CreateDtoValidator());
            if (EntityValidator() != null)
                await ValidatorHelper.ValidateAsync(entity, EntityValidator());
        }

        await BeforeCreateAsync(dto, entity);

        await EntityCreateHelper.AddAsync<TDbContext, TEntity, TKey>(DbContext, entity, UniqueProperties());

        await DbContext.SaveChangesAsync();

        var responseDto = (TCreateResponseDto) dto;
            
        MapEntityProperties.Map(entity, responseDto);

        await AfterCreateAsync(responseDto, entity);

        return responseDto;
    }

    public async Task<TUpdateResponseDto> UpdateAsync(TKey id, TUpdateDto dto)
    {
        await QueryHelper.ExistAndThrowAsync(Items, id);

        if (IsValidate)
        {
            if (IsValidateUsingAttributes)
                ValidatorHelper.Validate(dto);
            if (UpdateDtoValidator() != null)
                await ValidatorHelper.ValidateAsync(dto, UpdateDtoValidator());
        }

        var entity = await Items.Where($"{nameof(IEntity<TKey>.Id)} = @0", id).SingleAsync();

        Mapper.Map(dto, entity);

        if (IsValidate && EntityValidator() != null)
            await ValidatorHelper.ValidateAsync(entity, EntityValidator());

        await BeforeUpdateAsync(dto, entity);

        await EntityUpdateHelper.UpdateAsync<TDbContext, TEntity, TKey>(DbContext, entity, UniqueProperties());

        await DbContext.SaveChangesAsync();

        var responseDto = (TUpdateResponseDto) dto;
            
        MapEntityProperties.Map(entity, responseDto);

        await AfterUpdateAsync(responseDto, entity);

        return responseDto;
    }

    public async Task<TUpdateDto> ForUpdateAsync(TKey id)
    {
        EntityIdValidator.Validate(id);
            
        return await (await BeforeForUpdateAsync(Items.AsNoTracking()))
            .Where($"{nameof(IEntity<TKey>.Id)} = @0", id)
            .ProjectTo<TUpdateDto>(Mapper.ConfigurationProvider)
            .SingleOrDefaultAsync();
    }

    public async Task DeleteAsync(TKey id)
    {
        EntityIdValidator.Validate(id);

        await QueryHelper.ExistAndThrowAsync(Items, id);

        await BeforeDeleteAsync(id);

        var entity = typeof(ISoftDeleteEntity).IsAssignableFrom(typeof(TEntity))
            ? await Items.Where($"{nameof(IEntity<TKey>.Id)} = @0", id).SingleAsync()
            : new TEntity {Id = id};

        EntityDeleteHelper.Delete<TDbContext, TEntity, TKey>(DbContext, entity);

        await DbContext.SaveChangesAsync();

        await AfterDeleteAsync(id);
    }

    public async Task UndoDeleteAsync(TKey id)
    {
        if (!typeof(ISoftDeleteEntity).IsAssignableFrom(typeof(TEntity)))
            throw new Exception($"{typeof(TEntity).FullName} class not implement {nameof(ISoftDeleteEntity)}");

        EntityIdValidator.Validate(id);

        await BeforeUndoDeleteAsync(id);

        dynamic entity = await Items.IgnoreQueryFilters().Where($"{nameof(IEntity<TKey>.Id)} = @0", id)
            .SingleOrDefaultAsync();

        if (entity == null)
            throw new UserFriendlyException($"No {EntityHelper.EntityName<TEntity>()} found with id = {id}");

        entity.IsDeleted = false;

        Items.Update(entity);

        await DbContext.SaveChangesAsync();

        await AfterUndoDeleteAsync(id);
    }

    public async Task<TGetDto> GetAsync(TKey id)
    {
        EntityIdValidator.Validate(id);

        return await (await BeforeGetAsync(Items.AsNoTracking()))
            .Where($"{nameof(IEntity<TKey>.Id)} = @0", id)
            .ProjectTo<TGetDto>(Mapper.ConfigurationProvider)
            .SingleOrDefaultAsync();
    }

    protected abstract List<UniqueProperty> UniqueProperties();

    protected virtual AbstractValidator<TEntity> EntityValidator()
    {
        return null;
    }

    protected virtual AbstractValidator<TCreateDto> CreateDtoValidator()
    {
        return null;
    }

    protected virtual AbstractValidator<TUpdateDto> UpdateDtoValidator()
    {
        return null;
    }

    protected virtual async Task BeforeCreateAsync(TCreateDto dto, TEntity entity)
    {
        await Task.CompletedTask;
    }

    protected virtual async Task AfterCreateAsync(TCreateResponseDto dto, TEntity entity)
    {
        await Task.CompletedTask;
    }

    protected virtual async Task BeforeUpdateAsync(TUpdateDto dto, TEntity entity)
    {
        await Task.CompletedTask;
    }

    protected virtual async Task AfterUpdateAsync(TUpdateResponseDto dto, TEntity entity)
    {
        await Task.CompletedTask;
    }
        
    protected virtual async Task<IQueryable<TEntity>> BeforeForUpdateAsync(IQueryable<TEntity> query)
    {
        return await Task.FromResult(query);
    }

    protected virtual async Task BeforeDeleteAsync(TKey id)
    {
        await Task.CompletedTask;
    }

    protected virtual async Task AfterDeleteAsync(TKey id)
    {
        await Task.CompletedTask;
    }

    protected virtual async Task BeforeUndoDeleteAsync(TKey id)
    {
        await Task.CompletedTask;
    }

    protected virtual async Task AfterUndoDeleteAsync(TKey id)
    {
        await Task.CompletedTask;
    }

    protected virtual async Task<IQueryable<TEntity>> BeforeListAsync(IQueryable<TEntity> query)
    {
        return await Task.FromResult(query);
    }

    protected virtual async Task<IQueryable<TEntity>> BeforeGetAsync(IQueryable<TEntity> query)
    {
        return await Task.FromResult(query);
    }
}