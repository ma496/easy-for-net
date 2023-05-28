namespace EasyForNet.Application.Dto.Crud;

public class PagedResultRequest : LimitedResultRequest, IPagedResultRequest
{
    public int SkipCount { get; set; }
}