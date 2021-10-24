using System;

namespace EasyForNet.Application.Dto
{
    public abstract class Dto<TKey> : IDto<TKey>
        where TKey : IComparable
    {
        public virtual TKey Id { get; set; }
    }
}