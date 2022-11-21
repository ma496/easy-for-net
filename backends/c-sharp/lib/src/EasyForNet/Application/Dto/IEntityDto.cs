namespace EasyForNet.Application.Dto;

public interface IEntityDto<TKey>
{
    TKey Id { get; set; }
}