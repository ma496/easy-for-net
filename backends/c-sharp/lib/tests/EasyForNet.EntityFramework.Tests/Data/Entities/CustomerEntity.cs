using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using EasyForNet.Domain.Entities.Audit;

namespace EasyForNet.EntityFramework.Tests.Data.Entities;

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class CustomerEntity : SoftDeleteAuditEntity<long>
{
    public long Code { get; set; }

    [Required] public string IdCard { get; set; }

    [Required] public string Name { get; set; }

    [Required] public string CellNo { get; set; }
}