using System;

namespace EasyForNet.Application.Dto
{
    public class SoftDeleteDto<TKey> : Dto<TKey>, ISoftDeleteDto
        where TKey : IComparable
    {
        public bool IsDeleted { get; set; }
    }
}