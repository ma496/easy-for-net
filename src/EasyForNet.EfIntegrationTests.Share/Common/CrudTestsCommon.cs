using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using EasyForNet.Application.Dto;
using EasyForNet.Crud;
using EasyForNet.Domain.Entities;
using EasyForNet.Exceptions;
using EasyForNet.Extensions;
using EasyForNet.Tests.Share.Common;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace EasyForNet.EfIntegrationTests.Share.Common
{
    public abstract class
        CrudTestsCommon<TCrudActions, TEntity, TKey, TListDto, TCreateDto, TUpdateDto, TGetDto> : TestsCommon
        where TCrudActions : ICrudActions<TKey, TListDto, TCreateDto, TUpdateDto, TGetDto>
        where TEntity : IEntity<TKey>
        where TKey : IComparable
        where TListDto : class, IDto<TKey>
        where TCreateDto : class, IDto<TKey>
        where TUpdateDto : class
        where TGetDto : class, IDto<TKey>
    {
        protected CrudTestsCommon(ITestOutputHelper outputHelper) : base(outputHelper)
        {
        }

        protected abstract TCreateDto NewDto();

        protected virtual List<TCreateDto> NewDtos(int count)
        {
            return Enumerable.Range(0, count)
                .Select(_ => NewDto())
                .ToList();
        }

        protected TCreateDto NewDtoWrapper()
        {
            var dto = NewDto();

            if (dto is null)
                throw new Exception(
                    $"{nameof(NewDto)} method should return not null object of type {typeof(TCreateDto).FullName}");

            return dto;
        }

        protected List<TCreateDto> NewDtosWrapper(int count)
        {
            var dtos = NewDtos(count);

            if (dtos is null)
                throw new Exception(
                    $"{nameof(NewDtos)} method should return no null list of type {typeof(TCreateDto).FullName}");
            if (dtos.Count != count)
                throw new Exception(
                    $"{nameof(NewDtos)} method should return list of type {typeof(TCreateDto).FullName} with {count} objects");

            return dtos;
        }

        #region Internal Create Test

        protected delegate void BeforeCreateDelegate(TCreateDto dto);

        protected delegate void AfterCreateDelegate(TCreateDto dto, TGetDto createdDto);

        protected virtual async Task InternalCreateTestAsync(BeforeCreateDelegate beforeCreate = null,
            AfterCreateDelegate afterCreate = null)
        {
            var crudActions = Services.GetRequiredService<TCrudActions>();

            var dto = NewDtoWrapper();

            beforeCreate?.Invoke(dto);

            dto = await crudActions.CreateAsync(dto);

            crudActions = NewScopeService<TCrudActions>();

            var createdDto = await crudActions.GetAsync(dto.Id);

            CompareAssert(dto, createdDto);

            afterCreate?.Invoke(dto, createdDto);
        }

        #endregion

        #region Internal Create Duplicate Test

        protected delegate void BeforeCreateDuplicateDelegate(List<TCreateDto> dtos);

        protected delegate void AfterCreateDuplicateDelegate(List<TListDto> dtos);

        protected delegate string
            PrepareForCreateUniqueProperty(TCreateDto dto); // Modify dto and return unique property name

        protected virtual async Task InternalCreateDuplicateTest(int count, int uniquePropertiesCount,
            BeforeCreateDuplicateDelegate
                beforeCreateDuplicate = null, AfterCreateDuplicateDelegate afterCreateDuplicate = null,
            params PrepareForCreateUniqueProperty[] prepareForUniqueProperties)
        {
            Guard.Against.NegativeOrZero(count, nameof(count));
            Guard.Against.NegativeOrZero(uniquePropertiesCount, nameof(uniquePropertiesCount));
            Guard.Against.Null(prepareForUniqueProperties, nameof(prepareForUniqueProperties));
            Guard.Against.InvalidInput(uniquePropertiesCount, nameof(uniquePropertiesCount), v => v <= count,
                $"Value of {nameof(uniquePropertiesCount)} must be equal or less then {nameof(count)}");
            Guard.Against.InvalidInput(prepareForUniqueProperties.Length, nameof(prepareForUniqueProperties),
                v => v == uniquePropertiesCount,
                $"Length of {nameof(prepareForUniqueProperties)} must be equal to {nameof(uniquePropertiesCount)}");

            var crudActions = Services.GetRequiredService<TCrudActions>();

            var dtos = NewDtosWrapper(count);

            beforeCreateDuplicate?.Invoke(dtos);

            dtos = dtos.Select(d => crudActions.CreateAsync(d).Result).ToList();

            crudActions = NewScopeService<TCrudActions>();

            var createdDtos = (await crudActions.ListAsync())
                .Where(e => dtos.Select(d => d.Id).Contains(e.Id))
                .ToList();

            CompareAssert(dtos, createdDtos);

            afterCreateDuplicate?.Invoke(createdDtos);

            crudActions = NewScopeService<TCrudActions>();

            for (var i = 0; i < uniquePropertiesCount; i++)
            {
                var dto = dtos[i].Clone();
                dto.Id = default;

                var uniquePropertyName = prepareForUniqueProperties[i](dto);

                crudActions = NewScopeService<TCrudActions>();

                var exception = await Assert.ThrowsAsync<UniquePropertyException>(async () =>
                    await crudActions.CreateAsync(dto));

                Assert.Equal($"Duplicate of {uniquePropertyName} not allowed", exception.Message);

                OutputHelper.WriteLine(exception.Message);
            }
        }

        #endregion

        #region Internal Update Test

        protected delegate void BeforeUpdateDelegate(TUpdateDto dto);

        protected delegate void AfterUpdateDelegate(TUpdateDto dto, TGetDto updatedDto);

        protected virtual async Task InternalUpdateTestAsync(BeforeUpdateDelegate beforeUpdate = null,
            AfterUpdateDelegate afterUpdate = null)
        {
            var crudActions = Services.GetRequiredService<TCrudActions>();

            var dto = NewDtoWrapper();

            dto = await crudActions.CreateAsync(dto);

            crudActions = NewScopeService<TCrudActions>();

            var createdDto = await crudActions.GetAsync(dto.Id);

            CompareAssert(dto, createdDto);

            TUpdateDto forUpdateDto;
            if (typeof(TUpdateDto) != typeof(TGetDto))
            {
                forUpdateDto = Mapper.Map<TUpdateDto>(await crudActions.GetAsync(dto.Id));
            }
            else
                forUpdateDto = await crudActions.GetAsync(dto.Id) as TUpdateDto;

            beforeUpdate?.Invoke(forUpdateDto);

            forUpdateDto = await crudActions.UpdateAsync(dto.Id, forUpdateDto);

            crudActions = NewScopeService<TCrudActions>();

            var updatedDto = await crudActions.GetAsync(dto.Id);

            CompareAssert(forUpdateDto, updatedDto);

            afterUpdate?.Invoke(forUpdateDto, updatedDto);
        }

        #endregion

        #region Internal Update Duplicate Test

        protected delegate void BeforeUpdateDuplicateDelegate(List<TCreateDto> dtos);

        protected delegate void AfterUpdateDuplicateDelegate(List<TListDto> dtos);

        protected delegate string
            PrepareForUpdateUniqueProperty(TCreateDto dto,
                List<TCreateDto> dtos); // Modify dto and return unique property name

        protected virtual async Task InternalUpdateDuplicateTest(int count, int uniquePropertiesCount,
            BeforeUpdateDuplicateDelegate beforeUpdateDuplicate = null, AfterUpdateDuplicateDelegate
                afterUpdateDuplicate = null, params PrepareForUpdateUniqueProperty[] prepareForUniqueProperties)
        {
            Guard.Against.NegativeOrZero(count, nameof(count));
            Guard.Against.NegativeOrZero(uniquePropertiesCount, nameof(uniquePropertiesCount));
            Guard.Against.Null(prepareForUniqueProperties, nameof(prepareForUniqueProperties));
            Guard.Against.InvalidInput(uniquePropertiesCount, nameof(uniquePropertiesCount), v => v <= count,
                $"Value of {nameof(uniquePropertiesCount)} must be equal or less then {nameof(count)}");
            Guard.Against.InvalidInput(prepareForUniqueProperties.Length, nameof(prepareForUniqueProperties),
                v => v == uniquePropertiesCount,
                $"Length of {nameof(prepareForUniqueProperties)} must be equal to {nameof(uniquePropertiesCount)}");
            var crudActions = Services.GetRequiredService<TCrudActions>();

            var dtos = NewDtosWrapper(count);

            beforeUpdateDuplicate?.Invoke(dtos);

            dtos = dtos.Select(d => crudActions.CreateAsync(d).Result).ToList();

            crudActions = NewScopeService<TCrudActions>();

            var createdDtos = (await crudActions.ListAsync())
                .Where(e => dtos.Select(d => d.Id).Contains(e.Id))
                .ToList();

            CompareAssert(dtos, createdDtos);

            afterUpdateDuplicate?.Invoke(createdDtos);

            crudActions = NewScopeService<TCrudActions>();

            for (var i = 0; i < uniquePropertiesCount; i++)
            {
                var dto = dtos[i].Clone();

                var uniquePropertyName = prepareForUniqueProperties[i](dto, dtos);

                crudActions = NewScopeService<TCrudActions>();

                TUpdateDto forUpdateDto;
                if (typeof(TUpdateDto) == dto.GetType())
                    forUpdateDto = dto as TUpdateDto;
                else
                    forUpdateDto = Mapper.Map<TUpdateDto>(dto);

                var exception = await Assert.ThrowsAsync<UniquePropertyException>(async () =>
                    await crudActions.UpdateAsync(dto.Id, forUpdateDto));

                Assert.Equal($"Duplicate of {uniquePropertyName} not allowed", exception.Message);

                OutputHelper.WriteLine(exception.Message);
            }
        }

        #endregion

        #region Internal Delete Test

        protected delegate void BeforeDeleteDelegate(TKey id);

        protected delegate void AfterDeleteDelegate(TKey id);

        protected virtual async Task InternalDeleteTestAsync(BeforeDeleteDelegate beforeDelete = null,
            AfterDeleteDelegate afterDelete = null)
        {
            var crudActions = Services.GetRequiredService<TCrudActions>();

            var dto = NewDtoWrapper();

            dto = await crudActions.CreateAsync(dto);

            crudActions = NewScopeService<TCrudActions>();

            var createdDto = await crudActions.GetAsync(dto.Id);

            CompareAssert(dto, createdDto);

            beforeDelete?.Invoke(dto.Id);

            crudActions = NewScopeService<TCrudActions>();

            await crudActions.DeleteAsync(dto.Id);

            var deletedDto = await crudActions.GetAsync(dto.Id);

            Assert.Null(deletedDto);

            afterDelete?.Invoke(dto.Id);
        }

        #endregion

        #region Internal Undo Delete Test

        protected delegate void BeforeUndoDeleteDelegate(TKey id);

        protected delegate void AfterUndoDeleteDelegate(TKey id);

        protected virtual async Task InternalUndoDeleteTestAsync(BeforeUndoDeleteDelegate beforeUndoDelete = null,
            AfterUndoDeleteDelegate afterUndoDelete = null)
        {
            if (!typeof(ISoftDeleteEntity).IsAssignableFrom(typeof(TEntity)))
                throw new Exception($"{typeof(TEntity).FullName} class not implement {nameof(ISoftDeleteEntity)}");

            var crudActions = Services.GetRequiredService<TCrudActions>();

            var dto = NewDtoWrapper();
            dto = await crudActions.CreateAsync(dto);

            crudActions = NewScopeService<TCrudActions>();

            var createdDto = await crudActions.GetAsync(dto.Id);

            CompareAssert(dto, createdDto);

            await crudActions.DeleteAsync(dto.Id);

            crudActions = NewScopeService<TCrudActions>();

            var deletedDto = await crudActions.GetAsync(dto.Id);

            Assert.Null(deletedDto);

            beforeUndoDelete?.Invoke(dto.Id);

            await crudActions.UndoDeleteAsync(dto.Id);

            crudActions = NewScopeService<TCrudActions>();

            var undoDto = await crudActions.GetAsync(dto.Id);

            Assert.NotNull(undoDto);
            CompareAssert(createdDto, undoDto);

            afterUndoDelete?.Invoke(dto.Id);
        }

        #endregion

        #region Internal List Test

        protected delegate void BeforeListDelegate(List<TCreateDto> list);

        protected delegate void AfterListDelegate(List<TListDto> list);

        protected virtual async Task InternalListTestAsync(BeforeListDelegate beforeList = null,
            AfterListDelegate afterList = null)
        {
            var crudActions = Services.GetRequiredService<TCrudActions>();

            var dtos = NewDtosWrapper(10);

            beforeList?.Invoke(dtos);

            dtos = dtos.Select(d => crudActions.CreateAsync(d).Result).ToList();

            crudActions = NewScopeService<TCrudActions>();

            var createdDtos = (await crudActions.ListAsync())
                .Where(o => dtos.Select(d => d.Id).Contains(o.Id))
                .ToList();

            CompareAssert(dtos, createdDtos);

            afterList?.Invoke(createdDtos);
        }

        #endregion

        #region Internal Get Test

        protected delegate void BeforeGetDelegate(TCreateDto dto);

        protected delegate void AfterGetDelegate(TGetDto dto);

        protected virtual async Task InternalGetTestAsync(BeforeGetDelegate beforeGet = null,
            AfterGetDelegate afterGet = null)
        {
            var crudActions = Services.GetRequiredService<TCrudActions>();

            var dto = NewDtoWrapper();

            beforeGet?.Invoke(dto);

            dto = await crudActions.CreateAsync(dto);

            crudActions = NewScopeService<TCrudActions>();

            var createdDto = await crudActions.GetAsync(dto.Id);

            CompareAssert(dto, createdDto);

            afterGet?.Invoke(createdDto);
        }

        #endregion
    }
}