# Client — concise instructions

Apply when changing client libraries (`src/BookStore.Client/**`) or API consumers.

Key points:

- Use `*Dto` / `*Request` / `*Response` naming for transport models.
- Use `System.Text.Json` in .NET clients; JSON uses camelCase and enums as strings.
- Use typed `HttpClient`/`HttpClientFactory`; avoid hard-coded base URLs.
- Keep mapping between DTOs and domain models isolated in a mapper layer.
- Do not store secrets in code; use env vars or secret stores.
- Add integration/contract tests against a test API instance to verify serialization and status codes.

See `docs/api-client-generation.md` and `docs/api-conventions-guide.md` for details.

