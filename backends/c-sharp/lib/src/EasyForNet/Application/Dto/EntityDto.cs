using System;

namespace EasyForNet.Application.Dto
{
    public abstract class EntityDto<TKey> : IEntityDto<TKey>
        where TKey : IComparable
    {
        public virtual TKey Id { get; set; }
    }
}