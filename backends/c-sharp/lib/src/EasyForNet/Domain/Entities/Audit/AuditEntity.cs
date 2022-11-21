using System;
using System.ComponentModel.DataAnnotations;

namespace EasyForNet.Domain.Entities.Audit
{
    public abstract class AuditEntity<TKey> : Entity<TKey>, IAuditEntity
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
}