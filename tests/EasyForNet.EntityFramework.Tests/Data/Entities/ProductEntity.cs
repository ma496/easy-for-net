using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using EasyForNet.Domain.Entities.Audit;

namespace EasyForNet.EntityFramework.Tests.Data.Entities
{
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
    [SuppressMessage("ReSharper", "CollectionNeverUpdated.Global")]
    public class ProductEntity : AuditEntity<long>
    {
        public string Model { get; set; }

        public decimal Price { get; set; }

        public List<ProductItemEntity> Items { get; set; }
    }
}