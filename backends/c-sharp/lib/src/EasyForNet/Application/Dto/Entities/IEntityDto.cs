namespace EasyForNet.Application.Dto.Entities;

public interface IEntityDto<TKey>
{
    TKey Id { get; set; }
}