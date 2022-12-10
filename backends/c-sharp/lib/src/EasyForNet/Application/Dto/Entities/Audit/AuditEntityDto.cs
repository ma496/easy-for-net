using System;
using System.ComponentModel.DataAnnotations;

namespace EasyForNet.Application.Dto.Entities.Audit;

public abstract class AuditEntityDto<TKey> : EntityDto<TKey>, IAuditEntityDto
{
    [ScaffoldColumn(false)] public virtual DateTime CreatedAt { get; set; }


    [MaxLength(256)]
    [ScaffoldColumn(false)]
    public virtual string CreatedBy { get; set; }

    [ScaffoldColumn(false)] public virtual DateTime UpdatedAt { get; set; }

    [MaxLength(256)]
    [ScaffoldColumn(false)]
    public virtual string UpdatedBy { get; set; }
}