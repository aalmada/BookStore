namespace BookStore.ApiService.Infrastructure;

public record OrderedPagedRequest : PagedRequest
{
    public string? SortBy { get; init; }
    public string? SortOrder { get; init; } = "asc";
}
