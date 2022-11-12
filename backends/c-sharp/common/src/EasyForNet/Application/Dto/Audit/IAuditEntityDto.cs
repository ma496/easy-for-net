using System;

namespace EasyForNet.Application.Dto.Audit
{
    public interface IAuditEntityDto
    {
        DateTime CreatedAt { get; set; }

        string CreatedBy { get; set; }

        DateTime UpdatedAt { get; set; }

        string UpdatedBy { get; set; }
    }
}