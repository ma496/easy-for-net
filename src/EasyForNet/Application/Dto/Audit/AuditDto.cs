using System;
using System.ComponentModel.DataAnnotations;

namespace EasyForNet.Application.Dto.Audit
{
    public abstract class AuditDto<TKey> : Dto<TKey>, IAuditDto
        where TKey : IComparable
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