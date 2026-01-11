using System.Globalization;
using Microsoft.JSInterop;

namespace BookStore.Web.Services;

public class CurrencyService(IJSRuntime jsRuntime)
{
    const string CurrencyKey = "selected_currency";

    public string CurrentCurrency { get; private set; } = "USD";

    public event Action? OnCurrencyChanged;

    bool _initialized;

    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;

        var stored = await jsRuntime.InvokeAsync<string?>("localStorage.getItem", CurrencyKey);
        if (!string.IsNullOrEmpty(stored) && stored != CurrentCurrency)
        {
            CurrentCurrency = stored;
            OnCurrencyChanged?.Invoke();
        }
    }

    public async Task SetCurrencyAsync(string currency)
    {
        if (CurrentCurrency != currency)
        {
            CurrentCurrency = currency;
            await jsRuntime.InvokeVoidAsync("localStorage.setItem", CurrencyKey, currency);
            OnCurrencyChanged?.Invoke();
        }
    }

    public string FormatPrice(IReadOnlyDictionary<string, decimal>? prices)
    {
        if (prices == null || !prices.TryGetValue(CurrentCurrency, out var price))
        {
            return "N/A";
        }

        return CurrentCurrency switch
        {
            "USD" => string.Format(CultureInfo.InvariantCulture, "${0:N2}", price),
            "EUR" => string.Format(CultureInfo.InvariantCulture, "{0:N2}€", price),
            "GBP" => string.Format(CultureInfo.InvariantCulture, "£{0:N2}", price),
            _ => string.Format(CultureInfo.InvariantCulture, "{0:N2} {1}", price, CurrentCurrency)
        };
    }
}
