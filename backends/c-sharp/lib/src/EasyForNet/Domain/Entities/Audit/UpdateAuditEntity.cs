using System;
using System.ComponentModel.DataAnnotations;

namespace EasyForNet.Domain.Entities.Audit;

public abstract class UpdateAuditEntity<TKey> : Entity<TKey>, IUpdateAuditEntity
{
    public virtual DateTime UpdatedAt { get; set; }

    [MaxLength(256)]
    public virtual string UpdatedBy { get; set; }
}
