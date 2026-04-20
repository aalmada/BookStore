# Multi-Currency Guide

BookStore uses explicit per-currency pricing. Prices are authored and stored for each currency code (for example, USD/EUR/GBP), and the UI chooses which stored value to display.

## Current Implementation Summary

- Prices are stored as a dictionary keyed by ISO currency code.
- API configuration exposes supported currencies and default currency.
- The web UI lets users pick a currency and persists that choice in local storage.
- Price filtering supports a selected currency.
- Discounts are applied per stored currency price.
- Runtime foreign exchange conversion is not implemented.

## Configuration

Currency configuration is defined in ApiService settings:

```json
"Currency": {
  "DefaultCurrency": "GBP",
  "SupportedCurrencies": ["USD", "EUR", "GBP"]
}
```

`CurrencyOptions` validates:

- `DefaultCurrency` is required and must be a 3-character code.
- `SupportedCurrencies` must contain at least one value.
- `DefaultCurrency` must be present in `SupportedCurrencies`.

The API exposes this through `GET /api/config/currency`.

## How Prices Are Stored

### Write model (events and aggregate)

- Create/update commands carry `IReadOnlyDictionary<string, decimal>? Prices`.
- `BookHandlers` validates:
1. Default currency price is present.
2. All provided currencies are in configured supported currencies.
- `BookAggregate` stores `Dictionary<string, decimal> Prices` and validates non-empty dictionary, 3-character currency codes, and non-negative values.
- `BookAdded` and `BookUpdated` events persist the full dictionary.

### Read model

`BookSearchProjection` stores:

- `Prices` (base prices by currency)
- `CurrentPrices` (effective prices by currency after discount factor)

`CurrentPrices` is recalculated from `Prices` using the active discount percentage. This is discount calculation, not cross-currency conversion.

## DTOs and API Shape

Shared DTOs expose multi-currency prices:

- `BookDto.Prices` and `AdminBookDto.Prices`: dictionary by currency.
- `BookDto.CurrentPrices` and `AdminBookDto.CurrentPrices`: list of `PriceEntry` values representing effective prices (for example, after discount).
- Shopping cart item DTOs also expose a per-currency price dictionary.

## Currency Selection and Display in Web

`CurrencyService` is the client-side source of truth for selected currency:

- Default selected currency in the service is `GBP`.
- Selection is persisted under local storage key `selected_currency`.
- `OnCurrencyChanged` notifies UI components.
- `FormatPrice` formats known currencies (USD, EUR, GBP) using invariant numeric formatting and symbols.

`CurrencySelector` component:

- Is shown in the main app bar.
- Loads supported currencies from `GET /api/config/currency`.
- Uses a local fallback list (`USD`, `EUR`, `GBP`) if config fetch fails.

Pages such as home, book details, and cart render prices by looking up `CurrencyService.CurrentCurrency` in each book/cart `Prices` dictionary.

## Filtering by Currency

Book search supports currency-aware filtering:

- If only `Currency` is provided, results are filtered to books that have that currency in `Prices`.
- If `MinPrice` and/or `MaxPrice` are provided with `Currency`, filtering is applied to matching entries in `CurrentPrices`.
- Without a `Currency`, price range filtering applies across any `CurrentPrices` entry.

## Seeding Behavior

Database seeding currently generates USD, EUR, and GBP prices per book using category-based ranges and fixed multipliers plus charm endings (`.49`, `.95`, `.99`).

This seeding logic is only for initial/sample data generation and is not used for runtime conversion.

## Not Implemented (Important)

- No runtime FX/exchange-rate service exists.
- No background synchronization of prices from external currency providers exists.
- Currency switching does not convert values; it selects another stored value from the price dictionary.

## Practical Implications

- New currencies require both configuration updates and persisted per-book prices.
- If a selected currency is missing in a specific dictionary, UI formatting returns `N/A` for that item.
- For consistent UX, admin workflows should continue supplying prices for all supported currencies.
