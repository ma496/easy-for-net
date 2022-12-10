namespace EasyForNet.Application.Dto.Entities.Audit;

public class SoftDeleteAuditEntityDto<TKey> : AuditEntityDto<TKey>, ISoftDeleteEntityDto
{
    public bool IsDeleted { get; set; }
}