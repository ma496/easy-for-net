using System.Collections.Generic;

namespace EasyForNet.Application.Dto.Crud;

public class PagedResultDto<T> : ListResultDto<T>, IPagedResultDto<T>
{
    public long TotalCount { get; set; }

    public PagedResultDto()
    {
    }

    public PagedResultDto(IReadOnlyList<T> items, long totalCount) : base(items)
    {
        TotalCount = totalCount;
    }
}
