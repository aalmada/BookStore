# Bogus Usage Patterns in BookStore

## Project Patterns
- **Always use Bogus for test data generation in tests** (see tests/AGENTS.md, integration-testing-guide.md)
- **No hand-rolled random data**: All random/fake data in tests must use Bogus
- **Centralize generators**: Use shared helpers (e.g., FakeDataGenerators.cs) to avoid duplication
- **Unique data per test**: Use Bogus to ensure no test data conflicts
- **Locale support**: Use appropriate locale for multilingual data
- **No hardcoded test data**: All test data should be generated

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

## See also
- tests/BookStore.AppHost.Tests/Helpers/FakeDataGenerators.cs
- docs/guides/integration-testing-guide.md
