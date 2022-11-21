namespace EasyForNet.EntityFramework.Query;

public interface ISortAndFilterQueryParams
{
    string SortBy { get; }
    string Filter { get; }
}