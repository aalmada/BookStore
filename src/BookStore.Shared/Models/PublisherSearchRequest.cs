namespace BookStore.Shared.Models;

public record PublisherSearchRequest : OrderedPagedRequest
{
    public string? Search { get; init; }
}
