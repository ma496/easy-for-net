using System;
using EasyForNet.Application.Dto.Entities;
using EasyForNet.Domain.Entities;
using EasyForNet.EfIntegrationTests.Share.Common;
using EasyForNet.EntityFramework.Crud;
using Xunit;
using Xunit.Abstractions;

namespace EasyForNet.EntityFramework.Tests.Base;

[Collection(nameof(EasyForNetEntityFrameworkTestsCollectionFixture))]
public abstract class CrudTestsBase<TCrudActions, TEntity, TKey, TListDto, TCreateDto, TCreateResponseDto, TUpdateDto,
    TUpdateResponseDto, TGetDto> :
    CrudTestsCommon<TCrudActions, TEntity, TKey, TListDto, TCreateDto, TCreateResponseDto, TUpdateDto,
        TUpdateResponseDto, TGetDto>
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
    protected CrudTestsBase(ITestOutputHelper outputHelper) : base(outputHelper)
    {
    }
}