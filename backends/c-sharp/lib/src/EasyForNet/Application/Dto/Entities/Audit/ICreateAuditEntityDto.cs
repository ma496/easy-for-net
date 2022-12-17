using System;

namespace EasyForNet.Application.Dto.Entities.Audit;

public interface ICreateAuditEntityDto
{
    DateTime CreatedAt { get; set; }

    string CreatedBy { get; set; }
}
