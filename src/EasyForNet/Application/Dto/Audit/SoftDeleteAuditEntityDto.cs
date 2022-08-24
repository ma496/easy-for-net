using System;

namespace EasyForNet.Application.Dto.Audit
{
    public class SoftDeleteAuditEntityDto<TKey> : AuditEntityDto<TKey>, ISoftDeleteEntityDto
        where TKey : IComparable
    {
        public bool IsDeleted { get; set; }
    }
}