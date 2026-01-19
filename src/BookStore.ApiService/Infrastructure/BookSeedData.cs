
namespace BookStore.ApiService.Infrastructure;

public record BookSeedData(
    string Title,
    string Author,
    string Category,
    int Year,
    string Language,
    Dictionary<string, string> Descriptions
);
