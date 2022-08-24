using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using EasyForNet.Application.Dto;
using EasyForNet.Crud;
using EasyForNet.Domain.Entities;
using EasyForNet.Exceptions.UserFriendly;
using EasyForNet.Extensions;
using EasyForNet.Tests.Share.Common;
using EasyForNet.Tests.Share.Extensions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace EasyForNet.EfIntegrationTests.Share.Common
{
    public abstract class
        CrudTestsCommon<TCrudActions, TEntity, TKey, TListDto, TCreateDto, TCreateResponseDto, TUpdateDto,
            TUpdateResponseDto, TGetDto> : TestsCommon
        where TCrudActions : ICrudActions<TKey, TListDto, TCreateDto, TCreateResponseDto, TUpdateDto, TUpdateResponseDto
            , TGetDto>
        where TEntity : class, IEntity<TKey>, new()
        where TKey : IComparable
        where TListDto : class, IEntityDto<TKey>
        where TCreateDto : class
        where TCreateResponseDto : class, IEntityDto<TKey>, TCreateDto
        where TUpdateDto : class
        where TUpdateResponseDto : class, IEntityDto<TKey>, TUpdateDto
        where TGetDto : class, IEntityDto<TKey>
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

        protected delegate void AfterCreateDelegate(TCreateResponseDto dto);

        protected virtual async Task InternalCreateTestAsync(BeforeCreateDelegate beforeCreate = null,
            AfterCreateDelegate afterCreate = null)
        {
            var crudActions = Services.GetRequiredService<TCrudActions>();

            var dto = NewDtoWrapper();

            beforeCreate?.Invoke(dto);

            var responseDto = await crudActions.CreateAsync(dto);

            crudActions = NewScopeService<TCrudActions>();

            var createdDto = await crudActions.GetAsync(responseDto.Id);

            responseDto.Should().BeEquivalentTo(createdDto);

            afterCreate?.Invoke(responseDto);
        }

        #endregion

        #region Internal Create Duplicate Test

        protected delegate string
            PrepareForCreateUniqueProperty(TCreateDto dto); // Modify dto and return unique property name

        protected virtual async Task InternalCreateDuplicateTest(int count, int uniquePropertiesCount,
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

            var responseDtos = dtos.Select(d => crudActions.CreateAsync(d).Result).ToList();

            crudActions = NewScopeService<TCrudActions>();

            var createdDtos = (await crudActions.ListAsync())
                .Where(e => responseDtos.Select(d => d.Id).Contains(e.Id))
                .ToList();

            responseDtos.Should().BeEquivalentTo(createdDtos);

            crudActions = NewScopeService<TCrudActions>();

            for (var i = 0; i < uniquePropertiesCount; i++)
            {
                var responseDto = responseDtos[i].Clone();
                responseDto.Id = default;

                var uniquePropertyName = prepareForUniqueProperties[i](responseDto);

                crudActions = NewScopeService<TCrudActions>();

                var exception = await Assert.ThrowsAsync<UniquePropertyException>(async () =>
                    await crudActions.CreateAsync(responseDto));

                Assert.Equal($"Duplicate of {uniquePropertyName} not allowed", exception.Message);

                OutputHelper.WriteLine(exception.Message);
            }
        }

        #endregion

        #region Internal Update Test

        protected delegate void BeforeUpdateDelegate(TUpdateDto dto);

        protected delegate void AfterUpdateDelegate(TUpdateResponseDto dto);

        protected virtual async Task InternalUpdateTestAsync(BeforeUpdateDelegate beforeUpdate = null,
            AfterUpdateDelegate afterUpdate = null)
        {
            var crudActions = Services.GetRequiredService<TCrudActions>();

            var dto = NewDtoWrapper();

            var responseDto = await crudActions.CreateAsync(dto);

            crudActions = NewScopeService<TCrudActions>();

            var createdDto = await crudActions.GetAsync(responseDto.Id);

            responseDto.Should().BeEquivalentTo(createdDto);

            var forUpdateDto = await crudActions.ForUpdateAsync(responseDto.Id);

            beforeUpdate?.Invoke(forUpdateDto);

            var updateResponseDto = await crudActions.UpdateAsync(responseDto.Id, forUpdateDto);

            crudActions = NewScopeService<TCrudActions>();

            var updatedDto = await crudActions.GetAsync(responseDto.Id);

            updateResponseDto.Should().BeEquivalentTo(updatedDto);

            afterUpdate?.Invoke(updateResponseDto);
        }

        #endregion

        #region Internal Update Duplicate Test

        protected delegate string
            PrepareForUpdateUniqueProperty(TUpdateDto dto,
                List<TCreateDto> dtos); // Modify dto and return unique property name

        protected virtual async Task InternalUpdateDuplicateTest(int count, int uniquePropertiesCount,
            params PrepareForUpdateUniqueProperty[] prepareForUniqueProperties)
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

            var responseDtos = dtos.Select(d => crudActions.CreateAsync(d).Result).ToList();

            crudActions = NewScopeService<TCrudActions>();

            var createdDtos = (await crudActions.ListAsync())
                .Where(e => responseDtos.Select(d => d.Id).Contains(e.Id))
                .ToList();

            responseDtos.Should().BeEquivalentTo(createdDtos);

            crudActions = NewScopeService<TCrudActions>();

            for (var i = 0; i < uniquePropertiesCount; i++)
            {
                var responseDto = responseDtos[i].Clone();

                crudActions = NewScopeService<TCrudActions>();

                var forUpdateDto = await crudActions.ForUpdateAsync(responseDto.Id);
                
                var uniquePropertyName = prepareForUniqueProperties[i](forUpdateDto, dtos);

                var exception = await Assert.ThrowsAsync<UniquePropertyException>(async () =>
                    await crudActions.UpdateAsync(responseDto.Id, forUpdateDto));

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

            var responseDto = await crudActions.CreateAsync(dto);

            crudActions = NewScopeService<TCrudActions>();

            var createdDto = await crudActions.GetAsync(responseDto.Id);

            responseDto.Should().BeEquivalentTo(createdDto);

            beforeDelete?.Invoke(responseDto.Id);

            crudActions = NewScopeService<TCrudActions>();

            await crudActions.DeleteAsync(responseDto.Id);

            var deletedDto = await crudActions.GetAsync(responseDto.Id);

            Assert.Null(deletedDto);

            afterDelete?.Invoke(responseDto.Id);
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
            var responseDto = await crudActions.CreateAsync(dto);

            crudActions = NewScopeService<TCrudActions>();

            var createdDto = await crudActions.GetAsync(responseDto.Id);

            responseDto.Should().BeEquivalentTo(createdDto, opt => opt.BeCloseToDateTime(TimeSpan.FromSeconds(1)));

            await crudActions.DeleteAsync(responseDto.Id);

            crudActions = NewScopeService<TCrudActions>();

            var deletedDto = await crudActions.GetAsync(responseDto.Id);

            Assert.Null(deletedDto);

            beforeUndoDelete?.Invoke(responseDto.Id);

            await crudActions.UndoDeleteAsync(responseDto.Id);

            crudActions = NewScopeService<TCrudActions>();

            var undoDto = await crudActions.GetAsync(responseDto.Id);

            Assert.NotNull(undoDto);
            createdDto.Should().BeEquivalentTo(undoDto, opt => opt.BeCloseToDateTime(TimeSpan.FromSeconds(1)));

            afterUndoDelete?.Invoke(responseDto.Id);
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

            var responseDtos = dtos.Select(d => crudActions.CreateAsync(d).Result).ToList();

            crudActions = NewScopeService<TCrudActions>();

            var createdDtos = (await crudActions.ListAsync())
                .Where(o => responseDtos.Select(d => d.Id).Contains(o.Id))
                .ToList();

            responseDtos.Should().BeEquivalentTo(createdDtos);

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

            var responseDto = await crudActions.CreateAsync(dto);

            crudActions = NewScopeService<TCrudActions>();

            var createdDto = await crudActions.GetAsync(responseDto.Id);

            responseDto.Should().BeEquivalentTo(createdDto);

            afterGet?.Invoke(createdDto);
        }

        #endregion
    }
}