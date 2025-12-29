# BookStore.Shared

This project contains shared domain models, DTOs, value objects, and utilities used across both the backend (`BookStore.ApiService`) and frontend (`BookStore.Web`) projects.

## Purpose

The main goal of this library is to ensure type safety and consistency between the API and the Blazor client by sharing the same data structures.

## Key Components

### Value Objects
- **`PartialDate`**: A custom value object for handling incomplete dates (e.g., "2023", "2023-05"), used extensively for publication dates.

### Models & DTOs
- Contains Data Transfer Objects (DTOs) for books, authors, categories, etc.
- Enums and constants shared across the solution.
