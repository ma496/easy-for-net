namespace EasyForNet.Application.Dto
{
    public abstract class EntityDto<TKey> : IEntityDto<TKey>
    {
        public virtual TKey Id { get; set; }
    }
}