using System;

namespace EasyForNet.Application.Dto
{
    public interface IDto<TKey>
        where TKey : IComparable
    {
        TKey Id { get; set; }
    }
}