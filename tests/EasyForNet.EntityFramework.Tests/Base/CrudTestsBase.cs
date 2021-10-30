using System;
using EasyForNet.Application.Dto;
using EasyForNet.Crud;
using EasyForNet.Domain.Entities;
using EasyForNet.EfIntegrationTests.Share.Common;
using Xunit;
using Xunit.Abstractions;

namespace EasyForNet.EntityFramework.Tests.Base
{
    [Collection(nameof(EasyForNetEntityFrameworkTestsCollectionFixture))]
    public abstract class CrudTestsBase<TCrudActions, TEntity, TKey, TListDto, TCreateDto, TCreateResponseDto, TUpdateDto,
        TUpdateResponseDto, TGetDto> :
        CrudTestsCommon<TCrudActions, TEntity, TKey, TListDto, TCreateDto, TCreateResponseDto, TUpdateDto,
            TUpdateResponseDto, TGetDto>
        where TCrudActions : ICrudActions<TKey, TListDto, TCreateDto, TCreateResponseDto, TUpdateDto, TUpdateResponseDto
            , TGetDto>
        where TEntity : class, IEntity<TKey>, new()
        where TKey : IComparable
        where TListDto : class, IDto<TKey>
        where TCreateDto : class
        where TCreateResponseDto : class, IDto<TKey>, TCreateDto
        where TUpdateDto : class
        where TUpdateResponseDto : class, IDto<TKey>, TUpdateDto
        where TGetDto : class, IDto<TKey>
    {
        protected CrudTestsBase(ITestOutputHelper outputHelper) : base(outputHelper)
        {
        }
    }
}