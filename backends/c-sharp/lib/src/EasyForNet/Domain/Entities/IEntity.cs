namespace EasyForNet.Domain.Entities;

public interface IEntity<TKey>
{
    TKey Id { get; set; }

    bool IsTransient();
}