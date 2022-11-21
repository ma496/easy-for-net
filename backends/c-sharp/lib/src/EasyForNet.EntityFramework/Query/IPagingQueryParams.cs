namespace EasyForNet.EntityFramework.Query;

public interface IPagingQueryParams
{
    int Page { get; }
    int PageSize { get; }
}