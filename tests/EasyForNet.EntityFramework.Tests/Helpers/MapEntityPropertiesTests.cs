using System;
using System.Collections.Generic;
using AutoMapper;
using EasyForNet.Application.Dto.Audit;
using EasyForNet.Domain.Entities.Audit;
using EasyForNet.EntityFramework.Helpers;
using EasyForNet.EntityFramework.Tests.Base;
using Xunit;
using Xunit.Abstractions;

namespace EasyForNet.EntityFramework.Tests.Helpers
{
    public class MapEntityPropertiesTests : TestsBase
    {
        public MapEntityPropertiesTests(ITestOutputHelper outputHelper) : base(outputHelper)
        {
        }

        #region Tests

        [Fact]
        public void MapTest()
        {
            var productDto = new ProductDto
            {
                Model = "IPhone 13 Max",
                Category = new CategoryDto
                {
                    Name = "Mobile"
                },
                Items = new List<ProductItemDto>
                {
                    new()
                    {
                        SerialNo = $"1213_{IncrementalId.Id}"
                    },
                    new()
                    {
                        SerialNo = $"1213_{IncrementalId.Id}"
                    },
                    new()
                    {
                        SerialNo = $"1213_{IncrementalId.Id}"
                    }
                }
            };
            var productEntity = Mapper.Map<ProductEntity>(productDto);
            
            // Set entity id and audit properties
            productEntity.Id = 1;
            SetAuditProperties(productEntity);
            productEntity.Category.Id = 1;
            SetAuditProperties(productEntity.Category);
            foreach (var itemEntity in productEntity.Items)
            {
                itemEntity.Id = IncrementalId.Id;
                SetAuditProperties(itemEntity);
            }
            
            // Map entity id and audit properties to dto
            MapEntityProperties.Map(productEntity, productDto);
            
            CompareAssert(productEntity, productDto);
        }

        private void SetAuditProperties(IAuditEntity entity)
        {
            entity.CreatedAt = DateTime.Now;
            entity.CreatedBy = "Muhammad Ali";
            entity.UpdatedAt = DateTime.Now.AddMinutes(10);
            entity.UpdatedBy = "Muhammad Hammad";
        }

        #endregion

        #region Entities

        [AutoMap(typeof(ProductDto))]
        private class ProductEntity : AuditEntity<long>
        {
            public string Model { get; set; }

            public CategoryEntity Category { get; set; }

            public IList<ProductItemEntity> Items { get; set; }
        }
        
        [AutoMap(typeof(ProductItemDto))]
        private class ProductItemEntity : AuditEntity<long>
        {
            public string SerialNo { get; set; }
        }
        
        [AutoMap(typeof(CategoryDto))]
        private class CategoryEntity : AuditEntity<long>
        {
            public string Name { get; set; }
        }

        #endregion

        #region Dtos

        private class ProductDto : AuditDto<long>
        {
            public string Model { get; set; }

            public CategoryDto Category { get; set; }

            public IList<ProductItemDto> Items { get; set; }
        }

        private class ProductItemDto : AuditDto<long>
        {
            public string SerialNo { get; set; }
        }

        private class CategoryDto : AuditDto<long>
        {
            public string Name { get; set; }
        }

        #endregion
    }
}