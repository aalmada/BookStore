namespace BookStore.Shared.Models;

public record CategorySearchRequest : OrderedPagedRequest
{
    public string? Search { get; init; }
    public string? Language { get; init; }
}
