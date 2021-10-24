using System;
using EasyForNet.Application.Dto;
using EasyForNet.Crud;
using EasyForNet.Domain.Entities;
using EasyForNet.EfIntegrationTests.Share.Common;
using EasyForNet.EntityFramework.Crud;
using Xunit;
using Xunit.Abstractions;

namespace EasyForNet.EntityFramework.Tests.Base
{
    [Collection(nameof(EasyForNetEntityFrameworkTestsCollectionFixture))]
    public abstract class CrudTestsBase<TCrudActions, TEntity, TKey, TListDto, TCreateDto, TUpdateDto, TGetDto> :
        CrudTestsCommon<TCrudActions, TEntity, TKey, TListDto, TCreateDto, TUpdateDto, TGetDto>
        where TCrudActions : ICrudActions<TKey, TListDto, TCreateDto, TUpdateDto, TGetDto>
        where TEntity : IEntity<TKey>
        where TKey : IComparable
        where TListDto : class, IDto<TKey>
        where TCreateDto : class, IDto<TKey>
        where TUpdateDto : class
        where TGetDto : class, IDto<TKey>
    {
        protected CrudTestsBase(ITestOutputHelper outputHelper) : base(outputHelper)
        {
        }
    }
}