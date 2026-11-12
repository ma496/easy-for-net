namespace Backend.Base.Dto;

/// <summary>
/// Standard request DTO carrying pagination, sorting, search, and inclusion
/// criteria for list endpoints.
/// </summary>
public class ListRequestDto<TId>
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? SortField { get; set; }
    public SortDirection SortDirection { get; set; } = SortDirection.Asc;
    public string? Search { get; set; }
    public bool All { get; set; } = false;
    public HashSet<TId>? IncludeIds { get; set; }
}
