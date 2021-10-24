using System;

namespace EasyForNet.Domain.Entities.Audit
{
    public class SoftDeleteAuditEntity<TKey> : AuditEntity<TKey>, ISoftDeleteEntity
        where TKey : IComparable
    {
        public bool IsDeleted { get; set; }
    }
}