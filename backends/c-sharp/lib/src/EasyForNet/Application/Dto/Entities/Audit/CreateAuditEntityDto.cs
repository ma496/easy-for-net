using System;
using System.ComponentModel.DataAnnotations;

namespace EasyForNet.Application.Dto.Entities.Audit;

public abstract class CreateAuditEntityDto<TKey> : EntityDto<TKey>, ICreateAuditEntityDto
{
    public virtual DateTime CreatedAt { get; set; }

    [MaxLength(256)]
    public virtual string CreatedBy { get; set; }
}
