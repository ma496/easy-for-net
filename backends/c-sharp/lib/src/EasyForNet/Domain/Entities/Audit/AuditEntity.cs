using System;
using System.ComponentModel.DataAnnotations;

namespace EasyForNet.Domain.Entities.Audit;

public abstract class AuditEntity<TKey> : Entity<TKey>, IAuditEntity
{
    public virtual DateTime CreatedAt { get; set; }

    [MaxLength(256)]
    public virtual string CreatedBy { get; set; }

    public virtual DateTime UpdatedAt { get; set; }

    [MaxLength(256)]
    public virtual string UpdatedBy { get; set; }
}

public abstract class AuditEntity : Entity, IAuditEntity
{
    public virtual DateTime CreatedAt { get; set; }

    [MaxLength(256)]
    public virtual string CreatedBy { get; set; }

    public virtual DateTime UpdatedAt { get; set; }

    [MaxLength(256)]
    public virtual string UpdatedBy { get; set; }
}