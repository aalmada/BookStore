using Bogus;

namespace BookStore.AppHost.Tests;

/// <summary>
/// Provides helper methods to generate realistic fake data for integration tests using Bogus.
/// </summary>
public static class TestDataGenerators
{
    static readonly Faker _faker = new();

    public static object GenerateFakeBookRequest() => new
    {
        Title = _faker.Commerce.ProductName(),
        Isbn = _faker.Commerce.Ean13(),
        Language = "en",
        Translations = new Dictionary<string, object>
        {
            ["en"] = new { Description = _faker.Lorem.Paragraph() }
        },
#pragma warning disable IDE0037 // Use target-typed 'new' - not applicable for anonymous types
        PublicationDate = new
        {
            Year = _faker.Date.Past(10).Year,
            Month = _faker.Random.Int(1, 12),
            Day = _faker.Random.Int(1, 28)
        },
#pragma warning restore IDE0037
        PublisherId = (Guid?)null,
        AuthorIds = new Guid[] { },
        CategoryIds = new Guid[] { }
    };

    public static object GenerateFakeAuthorRequest() => new
    {
        Name = _faker.Name.FullName(),
        Translations = new Dictionary<string, object>
        {
            ["en"] = new
            {
                Biography = _faker.Lorem.Paragraphs(2)
            }
        }
    };

    public static object GenerateFakeCategoryRequest() => new
    {
        Translations = new Dictionary<string, object>
        {
            ["en"] = new
            {
                Name = _faker.Commerce.Department(),
                Description = _faker.Lorem.Sentence()
            }
        }
    };

    public static object GenerateFakePublisherRequest() => new
    {
        Name = _faker.Company.CompanyName()
    };
}
