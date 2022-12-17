using System;
using System.ComponentModel.DataAnnotations;

namespace EasyForNet.Application.Dto.Entities.Audit;

public abstract class UpdateAuditEntityDto<TKey> : EntityDto<TKey>, IUpdateAuditEntityDto
{
    public virtual DateTime UpdatedAt { get; set; }

    [MaxLength(256)]
    public virtual string UpdatedBy { get; set; }
}
