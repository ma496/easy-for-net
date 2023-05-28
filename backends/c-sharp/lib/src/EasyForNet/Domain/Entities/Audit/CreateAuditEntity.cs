using System;
using System.ComponentModel.DataAnnotations;

namespace EasyForNet.Domain.Entities.Audit;

public abstract class CreateAuditEntity<TKey> : Entity<TKey>, ICreateAuditEntity
{
    public virtual DateTime CreatedAt { get; set; }

    [MaxLength(256)]
    public virtual string CreatedBy { get; set; }
}

public abstract class CreateAuditEntity : Entity, ICreateAuditEntity
{
    public virtual DateTime CreatedAt { get; set; }

    [MaxLength(256)]
    public virtual string CreatedBy { get; set; }
}
