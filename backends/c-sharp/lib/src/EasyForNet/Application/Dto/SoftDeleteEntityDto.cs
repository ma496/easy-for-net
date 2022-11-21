namespace EasyForNet.Application.Dto
{
    public class SoftDeleteEntityDto<TKey> : EntityDto<TKey>, ISoftDeleteEntityDto
    {
        public bool IsDeleted { get; set; }
    }
}