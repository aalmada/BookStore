# Shared Library Instructions

**Scope**: `src/BookStore.Shared/**`

## Guides
- `docs/guides/localization-guide.md` - Localization patterns
- `docs/guides/real-time-notifications.md` - Notification models

## Rules
- No business logic - only contracts, models, DTOs, notifications
- Use `record` types with `init` accessors
- Define `IDomainEventNotification` implementations for SSE
- Ensure JSON-friendly (camelCase, ISO 8601)
