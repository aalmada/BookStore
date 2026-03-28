# Wolverine Testing & Troubleshooting

## Testing Handlers
- Handlers are pure functions, easy to unit test
- Use NSubstitute or similar for mocking dependencies
- Test edge cases and error handling

## Troubleshooting
- Handler not found: Check method name, static/public, assembly discovery
- Transaction not committing: Verify `.IntegrateWithWolverine()` and `AutoApplyTransactions()`
- ETag not working: Check `HttpContext` injection and header format

## Best Practices
- Keep commands simple (data only)
- Handlers are pure (no side effects except DB)
- Thin endpoints
- Document commands

See also: [wolverine-basics.md](wolverine-basics.md) and [wolverine-advanced.md](wolverine-advanced.md)
