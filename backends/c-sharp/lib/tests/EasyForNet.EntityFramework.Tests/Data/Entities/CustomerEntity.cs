using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using EasyForNet.Domain.Entities;
using EasyForNet.Domain.Entities.Audit;

namespace EasyForNet.EntityFramework.Tests.Data.Entities;

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class CustomerEntity : AuditEntity<long>, ISoftDeleteEntity
{
    public bool IsDeleted { get; set; }

    public long Code { get; set; }

    [Required] public string IdCard { get; set; }

    [Required] public string Name { get; set; }

    [Required] public string CellNo { get; set; }
}