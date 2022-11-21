namespace EasyForNet.Application.Dto.Audit
{
    public class SoftDeleteAuditEntityDto<TKey> : AuditEntityDto<TKey>, ISoftDeleteEntityDto
    {
        public bool IsDeleted { get; set; }
    }
}