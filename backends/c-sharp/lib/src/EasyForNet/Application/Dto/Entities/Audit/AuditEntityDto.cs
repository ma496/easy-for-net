using System;
using System.ComponentModel.DataAnnotations;

namespace EasyForNet.Application.Dto.Entities.Audit;

public abstract class AuditEntityDto<TKey> : EntityDto<TKey>, IAuditEntityDto
{
    public virtual DateTime CreatedAt { get; set; }


    [MaxLength(256)]
    public virtual string CreatedBy { get; set; }

    public virtual DateTime UpdatedAt { get; set; }

    [MaxLength(256)]
    public virtual string UpdatedBy { get; set; }
}

public abstract class AuditEntityDto : EntityDto, IAuditEntityDto
{
    public virtual DateTime CreatedAt { get; set; }


    [MaxLength(256)]
    public virtual string CreatedBy { get; set; }

    public virtual DateTime UpdatedAt { get; set; }

    [MaxLength(256)]
    public virtual string UpdatedBy { get; set; }
}