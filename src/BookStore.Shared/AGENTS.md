# Shared Library Instructions

**Scope**: `src/Shared/BookStore.Shared/**`

## Core Rules
- **Purity**: No business logic, only contracts, models, and shared utilities.
- **Serialization**: Ensure models are serializable/deserializable (JSON-friendly properties).
- **Notifications**: Define `IDomainEventNotification` implementations here for SSE.
- **DTOs**: Define request/response DTOs using `record` types.
- **Immutability**: Prefer `init` accessors and immutable collections.
