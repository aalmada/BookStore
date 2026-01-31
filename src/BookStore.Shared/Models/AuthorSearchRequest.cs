namespace BookStore.Shared.Models;

public record AuthorSearchRequest : OrderedPagedRequest
{
    public string? Search { get; init; }
}
