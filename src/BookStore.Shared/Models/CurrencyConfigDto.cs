namespace BookStore.Shared.Models;

/// <summary>
/// DTO for currency configuration
/// </summary>
public record CurrencyConfigDto(
    string DefaultCurrency,
    string[] SupportedCurrencies
);
