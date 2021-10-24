using System.Diagnostics.CodeAnalysis;
using EasyForNet.Domain.Entities.Audit;

namespace EasyForNet.EntityFramework.Tests.Data.Entities
{
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
    public class ProductItemEntity : AuditEntity<long>
    {
        public string SerialNo { get; set; }

        public long ProductId { get; set; }

        public ProductEntity Product { get; set; }
    }
}