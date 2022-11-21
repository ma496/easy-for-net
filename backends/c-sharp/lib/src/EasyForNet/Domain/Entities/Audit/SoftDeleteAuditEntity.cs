namespace EasyForNet.Domain.Entities.Audit;

public class SoftDeleteAuditEntity<TKey> : AuditEntity<TKey>, ISoftDeleteEntity
{
    public bool IsDeleted { get; set; }
}