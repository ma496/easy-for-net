namespace EasyForNet.EntityFramework.Query;

public interface IQueryParams : IPagingQueryParams, ISortAndFilterQueryParams
{
}

public class QueryParams : IQueryParams
{
    public QueryParams(int page, int pageSize, string sortBy, string filter)
    {
        Page = page;
        PageSize = pageSize;
        SortBy = sortBy;
        Filter = filter;
    }

    public int Page { get; }
    public int PageSize { get; }
    public string SortBy { get; }
    public string Filter { get; }
}