using System.ComponentModel.DataAnnotations;
using EasyForNet.Application.Dto.Entities.Audit;

namespace EasyForNet.EntityFramework.Tests.Crud.CustomerEntityCrudTests;

public class CustomerDto : SoftDeleteAuditEntityDto<long>
{
    public long Code { get; set; }

    [Required] public string Name { get; set; }

    public string IdCard { get; set; }

    public string CellNo { get; set; }
}