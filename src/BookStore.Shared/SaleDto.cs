namespace BookStore.Shared.Models;

public record SaleDto
{
    public Guid Id { get; init; }
    public string BookTitle { get; init; } = string.Empty;
    public string BuyerName { get; init; } = string.Empty;
    public DateTimeOffset Date { get; init; }
    public decimal Amount { get; init; }
    public string Status { get; init; } = string.Empty;
}
