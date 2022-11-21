namespace EasyForNet.Domain.Entities
{
    public class SoftDeleteEntity<TKey> : Entity<TKey>, ISoftDeleteEntity
    {
        public bool IsDeleted { get; set; }
    }
}