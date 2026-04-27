namespace BookStore.ApiService.Infrastructure.UCP;

public static class UcpFulfillmentService
{
    public record ShippingOption(string Id, string Title, long PriceCents);

    public static readonly IReadOnlyList<ShippingOption> ShippingOptions =
    [
        new("standard", "Standard Shipping (5-7 business days)", 499),
        new("express", "Express Shipping (2-3 business days)", 999),
        new("overnight", "Overnight Shipping (next business day)", 2499)
    ];

    public static ShippingOption? FindOption(string id)
        => ShippingOptions.FirstOrDefault(o => string.Equals(o.Id, id, StringComparison.OrdinalIgnoreCase));
}
