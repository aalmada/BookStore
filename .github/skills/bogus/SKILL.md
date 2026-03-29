---
name: bogus
description: Generate realistic fake data for .NET projects using the Bogus library. Use for test data, database seeding, randomized object creation, and prototyping. Always prefer Bogus over hand-rolled random data or hardcoded test values. Trigger for any .NET test, data seeding, or sample data scenario. Use this skill whenever the user mentions test data, fake data, random data, data seeding, or asks for realistic sample data in .NET, even if they don't mention Bogus by name. Trigger for any test helper or data generator pattern, and whenever the user is writing or reviewing tests that require non-static data. Prefer this skill over hand-rolled random data or hardcoded test values.
---

# Bogus Skill

This skill enables the AI to generate realistic fake data for .NET projects using the [Bogus](https://github.com/bchavez/Bogus) library. It is project-agnostic and supports C#, F#, and VB.NET.

## Key Patterns
- **Always use Bogus for test data in tests** (see [usage-patterns.md](references/usage-patterns.md))
- **No hand-rolled random data**: All random/fake data in tests must use Bogus
- **Centralize generators**: Use shared helpers to avoid duplication
- **Unique data per test**: Use Bogus to ensure no test data conflicts
- **Locale support**: Use appropriate locale for multilingual data
- **No hardcoded test data**: All test data should be generated

## Quick Start
See [bogus-api.md](references/bogus-api.md) for installation and usage.

## Example: Centralized Generator
```csharp
public static CreateBookRequest GenerateFakeBookRequest() => new()
{
    Id = Guid.CreateVersion7(),
    Title = _faker.Commerce.ProductName(),
    Isbn = _faker.Commerce.Ean13(),
    Language = "en",
    Translations = new Dictionary<string, BookTranslationDto>
    {
        ["en"] = new(_faker.Lorem.Paragraph()),
        ["es"] = new(_faker.Lorem.Paragraph())
    },
    PublicationDate = new PartialDate(
        _faker.Date.Past(10).Year,
        _faker.Random.Int(1, 12),
        _faker.Random.Int(1, 28)),
    Prices = new Dictionary<string, decimal> { ["USD"] = decimal.Parse(_faker.Commerce.Price(10, 100)) }
};
```

## References
- [usage-patterns.md](references/usage-patterns.md): Project usage patterns and best practices
- [bogus-api.md](references/bogus-api.md): API quick reference and installation
- [Bogus GitHub](https://github.com/bchavez/Bogus): Official documentation

## Advanced
- For extending Bogus, seeding EF Core, or analyzer setup, see [bogus-api.md](references/bogus-api.md)
- For project-specific conventions, see your project's AGENTS.md or test guides

---

**Keep this skill project-agnostic. Add new reference files for advanced scenarios as needed.**
