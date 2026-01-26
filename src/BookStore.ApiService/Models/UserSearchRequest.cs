using BookStore.ApiService.Infrastructure;

namespace BookStore.ApiService.Models;

public record UserSearchRequest : OrderedPagedRequest
{
    public string? Search { get; init; }
    public bool? IsAdmin { get; init; }
    public bool? EmailConfirmed { get; init; }
    public bool? HasPassword { get; init; }
    public bool? HasPasskey { get; init; }
}
