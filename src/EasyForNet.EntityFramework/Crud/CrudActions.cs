using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using EasyForNet.Application.Helpers;
using EasyForNet.Crud;
using EasyForNet.Domain.Entities;
using EasyForNet.EntityFramework.Data;
using EasyForNet.EntityFramework.Helpers;
using EasyForNet.Exceptions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace EasyForNet.EntityFramework.Crud
{
    public abstract class CrudActions<TDbContext, TEntity, TKey, TLisDto> :
        CrudActions<TDbContext, TEntity, TKey, TLisDto, TLisDto, TLisDto, TLisDto>
        where TDbContext : DbContextBase
        where TEntity : class, IEntity<TKey>, new()
        where TKey : IComparable
        where TLisDto : class
    {
        protected CrudActions(TDbContext dbContext, IMapper mapper, bool isValidate = false,
            bool isValidateUsingAttributes = true)
            : base(dbContext, mapper, isValidate, isValidateUsingAttributes)
        {
        }
    }

    public abstract class CrudActions<TDbContext, TEntity, TKey, TListDto, TCreateDto> :
        CrudActions<TDbContext, TEntity, TKey, TListDto, TCreateDto, TCreateDto, TListDto>
        where TDbContext : DbContextBase
        where TEntity : class, IEntity<TKey>, new()
        where TKey : IComparable
        where TListDto : class
        where TCreateDto : class
    {
        protected CrudActions(TDbContext dbContext, IMapper mapper, bool isValidate = false,
            bool isValidateUsingAttributes = true)
            : base(dbContext, mapper, isValidate, isValidateUsingAttributes)
        {
        }
    }

    public abstract class CrudActions<TDbContext, TEntity, TKey, TListDto, TCreateDto, TUpdateDto> :
        CrudActions<TDbContext, TEntity, TKey, TListDto, TCreateDto, TUpdateDto, TListDto>
        where TDbContext : DbContextBase
        where TEntity : class, IEntity<TKey>, new()
        where TKey : IComparable
        where TListDto : class
        where TCreateDto : class
        where TUpdateDto : class
    {
        protected CrudActions(TDbContext dbContext, IMapper mapper, bool isValidate = false,
            bool isValidateUsingAttributes = true)
            : base(dbContext, mapper, isValidate, isValidateUsingAttributes)
        {
        }
    }

    public abstract class CrudActions<TDbContext, TEntity, TKey, TListDto, TCreateDto, TUpdateDto, TGetDto>
        : ICrudActions<TKey, TListDto, TCreateDto, TUpdateDto, TGetDto>
        where TDbContext : DbContextBase
        where TEntity : class, IEntity<TKey>, new()
        where TKey : IComparable
        where TListDto : class
        where TCreateDto : class
        where TUpdateDto : class
        where TGetDto : class
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

        public async Task<TCreateDto> CreateAsync(TCreateDto dto)
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

            MapEntityProperties.Map(entity, dto);

            await AfterCreateAsync(dto, entity);

            return dto;
        }

        public async Task<TUpdateDto> UpdateAsync(TKey id, TUpdateDto dto)
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

            MapEntityProperties.Map(entity, dto);

            await AfterUpdateAsync(dto, entity);

            return dto;
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
                throw new AppException($"No {EntityHelper.EntityName(typeof(TEntity))} found with id = {id}");

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

        protected virtual async Task AfterCreateAsync(TCreateDto dto, TEntity entity)
        {
            await Task.CompletedTask;
        }

        protected virtual async Task BeforeUpdateAsync(TUpdateDto dto, TEntity entity)
        {
            await Task.CompletedTask;
        }

        protected virtual async Task AfterUpdateAsync(TUpdateDto dto, TEntity entity)
        {
            await Task.CompletedTask;
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
}