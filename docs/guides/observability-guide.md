# Observability and Usage Tracking Guide

This guide describes how observability and usage metrics are implemented in the BookStore application using OpenTelemetry and the .NET `System.Diagnostics.Metrics` APIs.

## Overview

The BookStore application tracks key business events (usage metrics) and infrastructure telemetry using OpenTelemetry. Metrics, traces, and logs are visible in the Aspire Dashboard and can be exported to OTLP-compatible collectors.

## What Is Instrumented

- **Custom business metrics** from `BookStore.ApiService` meter
- **Runtime metrics** (`AddRuntimeInstrumentation`)
- **ASP.NET Core + HttpClient metrics/traces**
- **Wolverine metrics** via `AddMeter("Wolverine")`
- **Structured logs** via OpenTelemetry logging provider
- **Health checks** via `/health` and `/alive`

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

Distributed tracing and correlation/causation propagation are documented in [Correlation and Causation Guide](correlation-causation-guide.md).

Structured logging patterns are documented in [Logging Guide](logging-guide.md).

## Implementation Details

The metrics are defined in the `BookStore.ApiService.Infrastructure.Instrumentation` static class.

Registration is performed in `BookStore.ServiceDefaults/Extensions.cs`:
```csharp
.WithMetrics(metrics => metrics
    // ... other instrumentation
    .AddRuntimeInstrumentation()
    .AddMeter("Wolverine")
    .AddMeter("BookStore.ApiService"))
```

OpenTelemetry logging is also enabled through `builder.Logging.AddOpenTelemetry(...)` in the same file.

## Exporters

Telemetry exporters are configured in `BookStore.ServiceDefaults/Extensions.cs`:

- **OTLP exporter** when `OTEL_EXPORTER_OTLP_ENDPOINT` is set
- Optional commented examples for **Azure Monitor** and **Prometheus** exporters

## Health and Endpoints

`BookStore.ServiceDefaults` configures:

- `/health` for readiness checks
- `/alive` for liveness checks

These endpoints are excluded from global rate limiting and are safe for probes.

## Viewing Metrics

1. Start the application using Aspire.
2. Open the **Aspire Dashboard** (usually at `http://localhost:18888`).
3. Navigate to the **Metrics** tab.
4. Select the `BookStore.ApiService` resource.
5. Choose the metric you want to visualize from the dropdown.
6. Use the **Filters** to drill down by `tenant_id`.

You can also filter logs and traces per resource in Aspire Dashboard to correlate latency spikes with specific endpoints or background handlers.
