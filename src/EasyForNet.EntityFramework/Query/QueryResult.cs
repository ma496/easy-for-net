using System.Collections;

namespace EasyForNet.EntityFramework.Query
{
    public class QueryResult<T>
        where T : IEnumerable
    {
        public QueryResult(long total, T data, int page, int pageSize)
        {
            Total = total;
            Data = data;
            Page = page;
            PageSize = pageSize;
        }

        public long Total { get; }
        public T Data { get; }
        public int Page { get; }
        public int PageSize { get; }
    }
}