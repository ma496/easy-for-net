using System;

namespace EasyForNet.Domain.Entities
{
    public interface IEntity<TKey>
        where TKey : IComparable
    {
        TKey Id { get; set; }

        bool IsTransient();
    }
}