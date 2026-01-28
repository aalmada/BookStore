# Observability and Usage Tracking Guide

This guide describes how observability and usage metrics are implemented in the BookStore application using OpenTelemetry and the .NET `System.Diagnostics.Metrics` APIs.

## Overview

The BookStore application tracks key business events (usage metrics) to provide insights into user behavior and system performance. These metrics are visible in the Aspire Dashboard and can be exported to other OpenTelemetry-compatible collectors (e.g., Prometheus, Azure Monitor).

## Core Metrics

All custom metrics are registered under the meter name `BookStore.ApiService`.

### Book Interactions
- **`bookstore.books.views`** (Counter): Incremented every time a book's details are retrieved via `/api/books/{id}`.
- **`bookstore.books.searches`** (Counter): Incremented every time a search is performed via `/api/books`.
- **`bookstore.books.search_duration`** (Histogram): Tracks the latency (in milliseconds) of book search queries.

### User Actions
- **`bookstore.users.favorites.added`** (Counter): Incremented when a user adds a book to their favorites.
- **`bookstore.users.ratings.added`** (Counter): Incremented when a user submits a rating for a book.
- **`bookstore.users.cart.added`** (Counter): Incremented when items are added to a shopping cart.
- **`bookstore.users.cart.removed`** (Counter): Incremented when items are removed from a shopping cart.

### Backoffice Operations
- **`bookstore.sales.scheduled`** (Counter): Incremented when a new sale is scheduled for a book.
- **`bookstore.sales.canceled`** (Counter): Incremented when a scheduled sale is canceled.

## Multi-Tenancy

Every metric includes a `tenant_id` tag. This allows you to filter and group metrics by tenant in the Aspire Dashboard or other monitoring tools.

## Implementation Details

The metrics are defined in the `BookStore.ApiService.Infrastructure.Instrumentation` static class.

Registration is performed in `BookStore.ServiceDefaults/Extensions.cs`:
```csharp
.WithMetrics(metrics => metrics
    // ... other instrumentation
    .AddMeter("BookStore.ApiService"))
```

## Viewing Metrics

1. Start the application using Aspire.
2. Open the **Aspire Dashboard** (usually at `http://localhost:18888`).
3. Navigate to the **Metrics** tab.
4. Select the `BookStore.ApiService` resource.
5. Choose the metric you want to visualize from the dropdown.
6. Use the **Filters** to drill down by `tenant_id`.
