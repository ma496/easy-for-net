using System;

namespace EasyForNet.Application.Dto.Audit
{
    public class SoftDeleteAuditDto<TKey> : AuditDto<TKey>, ISoftDeleteDto
        where TKey : IComparable
    {
        public bool IsDeleted { get; set; }
    }
}