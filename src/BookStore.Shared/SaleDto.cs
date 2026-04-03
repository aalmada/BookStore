namespace BookStore.Shared.Models;

public record SaleDto
{
    public Guid BookId { get; init; }
    public string BookTitle { get; init; } = string.Empty;
    public decimal Percentage { get; init; }
    public DateTimeOffset Start { get; init; }
    public DateTimeOffset End { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? BookETag { get; init; }
}
