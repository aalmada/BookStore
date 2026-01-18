# Multi-Currency Pricing Guide

The Book Store application supports multi-currency pricing, allowing for explicit price control across different regions and a tailored shopping experience for international users.

## Architectural Philosophy

We chose **Explicit Pricing** over dynamic conversion for several strategic reasons:

*   **Psychological Pricing**: Regional markets have different price thresholds. A book priced at $19.99 is better represented as €17.99 or £14.99 rather than an unoptimized conversion like €18.43.
*   **Stability**: Item prices in the database remain constant, shielding the application from volatile exchange rate fluctuations during the checkout process.
*   **Performance**: Since prices are denormalized into the `BookSearchProjection`, the UI can display them instantly without contacting an external Pricing API.

## Interesting Facts & Technical Insights

### 1. Architectural Symmetry
The multi-currency system is designed to mirror the **Localization Pattern** used throughout the codebase. 
- Just as we use a `Dictionary<string, string>` for translated text (e.g., `Title`), we use a `Dictionary<string, decimal>` for `Prices`. 
- The same validation logic ensures that the `DefaultCurrency` (configured in `appsettings.json`) is always present, just like the default language translation.

### 2. Intelligent Category-Aware Seeding
The `DatabaseSeeder` doesn't just assign random numbers. It implements a realistic pricing strategy based on book categories:
- **Classics**: Priced defensively ($7–$13) as they are often in the public domain or have lower licensing costs.
- **History & Professional**: Priced higher ($20–$41) to reflect specialized content value.
- **Sci-Fi & Fantasy**: Mid-tier pricing ($15–$31).

### 3. Psychological Pricing Endings
To mimic real-world retail behavior, the seeder applies "charm pricing" logic. Every generated price is terminated with a psychological ending:
- **.99**: The most common "sale" or retail ending.
- **.49**: Suggests a value-oriented price.
- **.95**: Often used in premium or "boutique" listings.

### 4. Blazor Prerendering Safety
Accessing `localStorage` for user preferences (like currency) can break Blazor Server applications during the static prerendering phase (where no browser/JS context exists). 
We solved this by:
1.  Initializing the `CurrencyService` in the `OnAfterRenderAsync` lifecycle method.
2.  Using an initialization guard to prevent redundant calls.
3.  Ensuring the UI reactively updates via the `OnCurrencyChanged` event once the browser-side initialization completes.

## Configuration

Available currencies are defined in the `ApiService` via `appsettings.json`:

```json
"Currency": {
  "DefaultCurrency": "USD",
  "SupportedCurrencies": ["USD", "EUR", "GBP"]
}
```

## UI Implementation

The `CurrencyService` in the Web project acts as the single source of truth:
- **State Management**: It tracks `CurrentCurrency`.
- **Persistence**: It keeps the choice in `localStorage`.
- **Formatting**: It provides `FormatPrice(prices)` which uses `CultureInfo.InvariantCulture` to ensure consistent formatting (using dots as decimal separators) across all server locales.
