using System.Text.Json.Serialization;

namespace BookStore.ApiService.Models.Ucp;

public sealed record UcpCatalogSearchResponse(
    [property: JsonPropertyName("items")] List<UcpCatalogItem> Items,
    [property: JsonPropertyName("total_count")] int TotalCount,
    [property: JsonPropertyName("has_more")] bool HasMore);

public sealed record UcpCatalogItem(
    string Id,
    string Title,
    string? Description,
    string? Isbn,
    [property: JsonPropertyName("author_names")] string? AuthorNames,
    [property: JsonPropertyName("publisher_name")] string? PublisherName,
    UcpCatalogPrice Price,
    string Availability,
    [property: JsonPropertyName("cover_url")] string? CoverUrl);

public sealed record UcpCatalogPrice(
    string Currency,
    long Amount);
