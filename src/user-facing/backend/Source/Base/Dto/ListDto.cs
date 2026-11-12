namespace Backend.Base.Dto;

/// <summary>
/// Generic paged list response wrapper containing the items for the current
/// page and the total count across all pages.
/// </summary>
public class ListDto<T>
{
    public List<T> Items { get; set; } = [];
    public int Total { get; set; }
}