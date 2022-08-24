using System;

namespace EasyForNet.Application.Dto
{
    public interface IEntityDto<TKey>
        where TKey : IComparable
    {
        TKey Id { get; set; }
    }
}