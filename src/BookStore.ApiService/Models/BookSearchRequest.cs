using BookStore.ApiService.Infrastructure;

namespace BookStore.ApiService.Models;

public record BookSearchRequest : OrderedPagedRequest
{
    public string? Search { get; init; }
    public Guid? AuthorId { get; init; }
    public Guid? CategoryId { get; init; }
    public Guid? PublisherId { get; init; }
    public bool? OnSale { get; init; }
    public decimal? MinPrice { get; init; }
    public decimal? MaxPrice { get; init; }
}
