using System.ComponentModel.DataAnnotations;
using EasyForNet.Application.Dto.Audit;

namespace EasyForNet.EntityFramework.Tests.Crud.CustomerEntityCrudTests
{
    public class CustomerDto : SoftDeleteAuditDto<long>
    {
        public long Code { get; set; }

        [Required] public string Name { get; set; }

        public string IdCard { get; set; }

        public string CellNo { get; set; }
    }
}