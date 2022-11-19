using System;

namespace EasyForNet.Application.Dto
{
    public class SoftDeleteEntityDto<TKey> : EntityDto<TKey>, ISoftDeleteEntityDto
        where TKey : IComparable
    {
        public bool IsDeleted { get; set; }
    }
}