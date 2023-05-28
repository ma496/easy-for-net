using System;

namespace EasyForNet.Application.Dto.Entities.Audit;

public interface IUpdateAuditEntityDto
{
    DateTime UpdatedAt { get; set; }

    string UpdatedBy { get; set; }
}
