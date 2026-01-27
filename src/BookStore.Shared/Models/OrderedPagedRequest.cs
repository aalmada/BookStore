namespace BookStore.Shared.Models;

public record OrderedPagedRequest : PagedRequest
{
    public string? SortBy { get; init; }
    public string? SortOrder { get; init; } = "asc";
}
