namespace EasyForNet.Application.Dto.Entities;

public class SoftDeleteEntityDto<TKey> : EntityDto<TKey>, ISoftDeleteEntityDto
{
    public bool IsDeleted { get; set; }
}