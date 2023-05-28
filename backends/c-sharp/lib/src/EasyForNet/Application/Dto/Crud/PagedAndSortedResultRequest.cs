namespace EasyForNet.Application.Dto.Crud;

public class PagedAndSortedResultRequest : PagedResultRequest, IPagedAndSortedResultRequest
{
    public string Sorting { get; set; }
}
