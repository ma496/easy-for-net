using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using EasyForNet.Application.Dependencies;
using EasyForNet.Application.Dto.Audit;
using EasyForNet.EntityFramework.Crud;
using EasyForNet.EntityFramework.Helpers;
using EasyForNet.EntityFramework.Tests.Base;
using EasyForNet.EntityFramework.Tests.Data;
using EasyForNet.EntityFramework.Tests.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;

namespace EasyForNet.EntityFramework.Tests.Crud
{
    public class ProductEntityCrudTests : CrudTestsBase<ProductEntityCrudActions, ProductEntity, long, ProductDto,
        ProductDto, ProductDto, ProductDto, ProductDto, ProductDto>
    {
        public ProductEntityCrudTests(ITestOutputHelper outputHelper) : base(outputHelper)
        {
        }

        [Fact]
        public async Task CreateTest()
        {
            await InternalCreateTestAsync(null, dto => Assert.Equal(10, dto.Items.Count));
        }

        [Fact]
        public async Task UpdateTest()
        {
            await InternalUpdateTestAsync(dto =>
            {
                dto.Model = $"model : {IncrementalId.Id}";
                dto.Items = dto.Items.Take(6).Concat(NewProductItems(5)).ToList();
                dto.Items[0].SerialNo = $"serial no: {IncrementalId.Id}";
            }, updatedDto => Assert.Equal(11, updatedDto.Items.Count));
        }

        [Fact]
        public async Task DeleteTest()
        {
            await InternalDeleteTestAsync();
        }

        protected override ProductDto NewDto()
        {
            return new()
            {
                Model = $"model:{IncrementalId.Id}",
                Price = 20000,
                Items = NewProductItems(10)
            };
        }

        private List<ProductItemDto> NewProductItems(int itemCount)
        {
            return Enumerable.Range(0, itemCount)
                .Select(_ => new ProductItemDto
                {
                    SerialNo = $"serial no : {IncrementalId.Id}",
                }).ToList();
        }
    }

    public class ProductEntityCrudActions :
        CrudActions<EasyForNetEntityFrameworkTestsDb, ProductEntity, long, ProductDto, ProductDto, ProductDto,
        ProductDto, ProductDto, ProductDto>,
        IScopedDependency
    {
        public ProductEntityCrudActions(EasyForNetEntityFrameworkTestsDb dbContext, IMapper mapper) : base(dbContext,
            mapper, true)
        {
        }

        protected override async Task BeforeUpdateAsync(ProductDto dto, ProductEntity entity)
        {
            var productItems = DbContext.ProductItems
                .AsNoTracking()
                .Where(pi => pi.ProductId == dto.Id)
                .ToList();
            await EntityHelper.RemoveRowsAsync(DbContext.ProductItems, productItems, dto.Items,
                (e, d) => e.Id == d.Id, d => Mapper.Map<ProductItemEntity>(d));
        }

        protected override async Task BeforeDeleteAsync(long id)
        {
            var productItems = await DbContext.ProductItems
                .Where(pi => pi.ProductId == id)
                .Select(pi => new ProductItemEntity
                {
                    Id = pi.Id
                })
                .ToListAsync();

            DbContext.ProductItems.RemoveRange(productItems);
        }

        protected override List<UniqueProperty> UniqueProperties()
        {
            return new()
            {
                new UniqueProperty(nameof(ProductDto.Model))
            };
        }
    }

    public class ProductDto : AuditEntityDto<long>
    {
        public string Model { get; set; }

        public decimal Price { get; set; }

        public List<ProductItemDto> Items { get; set; }
    }

    public class ProductItemDto : AuditEntityDto<long>
    {
        public string SerialNo { get; set; }
    }

    public class ProductEntityCrudTestsProfile : Profile
    {
        public ProductEntityCrudTestsProfile()
        {
            CreateMap<ProductDto, ProductEntity>(MemberList.Source);
            CreateMap<ProductEntity, ProductDto>();
            CreateMap<ProductItemDto, ProductItemEntity>(MemberList.Source);
            CreateMap<ProductItemEntity, ProductItemDto>();
        }
    }
}