using System;

namespace EasyForNet.Domain.Entities
{
    public class SoftDeleteEntity<TKey> : Entity<TKey>, ISoftDeleteEntity
        where TKey : IComparable
    {
        public bool IsDeleted { get; set; }
    }
}