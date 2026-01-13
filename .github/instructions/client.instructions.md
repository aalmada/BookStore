---
applyTo: "src/BookStore.Client/**"
---

# Client path-specific instructions

Apply to: `src/BookStore.Client/**`.

Essentials:

- Use `*Dto`/`*Request`/`*Response` naming for transport models.
- Use `System.Text.Json` in .NET clients; JSON is camelCase and enums as strings.
- Use typed `HttpClient`/`HttpClientFactory`; keep base URLs configurable (settings/env).
- Keep mapping between DTOs and domain models in a mapper layer.
- Do not store secrets in code; use environment variables or secret stores.
- Add contract or integration tests against a test API instance.

See `docs/api-client-generation.md` and `docs/api-conventions-guide.md` for details.
