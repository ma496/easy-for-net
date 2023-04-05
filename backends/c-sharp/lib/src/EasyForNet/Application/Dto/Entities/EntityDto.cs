namespace EasyForNet.Application.Dto.Entities;

public abstract class EntityDto<TKey> : IEntityDto<TKey>
{
    public virtual TKey Id { get; set; }
}

public abstract class EntityDto : IEntityDto
{
    
}