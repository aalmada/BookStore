using Bogus;
using BookStore.Client;
using BookStore.Shared.Models;

namespace BookStore.AppHost.Tests.Helpers;

public static class FakeDataGenerators
{
    static readonly Faker _faker = new();

    /// <summary>
    /// Generates a random password that meets common password requirements.
    /// </summary>
    /// <returns>A password with at least 12 characters including uppercase, lowercase, numbers, and special characters.</returns>
    public static string GenerateFakePassword() => _faker.Internet.Password(12, false, "", "Aa1!");

    /// <summary>
    /// Generates a random email address for testing.
    /// </summary>
    /// <returns>A valid email address.</returns>
    public static string GenerateFakeEmail() => _faker.Internet.Email();

    /// <summary>
    /// Generates a fake book creation request with random data using Bogus.
    /// </summary>
    /// <param name="publisherId">Optional publisher ID. If null, the book will have no publisher.</param>
    /// <param name="authorIds">Optional collection of author IDs. If null or empty, the book will have no authors.</param>
    /// <param name="categoryIds">Optional collection of category IDs. If null or empty, the book will have no categories.</param>
    /// <returns>A CreateBookRequest with randomized title, ISBN, translations, and prices.</returns>
    public static CreateBookRequest
        GenerateFakeBookRequest(Guid? publisherId = null, IEnumerable<Guid>? authorIds = null,
            IEnumerable<Guid>? categoryIds = null) => new()
            {
                Id = Guid.CreateVersion7(),
                Title = _faker.Commerce.ProductName(),
                Isbn = _faker.Commerce.Ean13(),
                Language = "en",
                Translations =
            new Dictionary<string, BookTranslationDto>
            {
                ["en"] = new(_faker.Lorem.Paragraph()),
                ["es"] = new(_faker.Lorem.Paragraph())
            },
                PublicationDate = new PartialDate(
            _faker.Date.Past(10).Year,
            _faker.Random.Int(1, 12),
            _faker.Random.Int(1, 28)),
                PublisherId = publisherId,
                AuthorIds = [.. (authorIds ?? [])],
                CategoryIds = [.. (categoryIds ?? [])],
                Prices = new Dictionary<string, decimal> { ["USD"] = decimal.Parse(_faker.Commerce.Price(10, 100)) }
            };

    public static UpdateBookRequest
        GenerateFakeUpdateBookRequest(Guid? publisherId = null, IEnumerable<Guid>? authorIds = null,
            IEnumerable<Guid>? categoryIds = null) => new()
            {
                Title = _faker.Commerce.ProductName(),
                Isbn = _faker.Commerce.Ean13(),
                Language = "en",
                Translations =
            new Dictionary<string, BookTranslationDto>
            {
                ["en"] = new(_faker.Lorem.Paragraph()),
                ["es"] = new(_faker.Lorem.Paragraph())
            },
                PublicationDate = new PartialDate(
            _faker.Date.Past(10).Year,
            _faker.Random.Int(1, 12),
            _faker.Random.Int(1, 28)),
                PublisherId = publisherId,
                AuthorIds = [.. (authorIds ?? [])],
                CategoryIds = [.. (categoryIds ?? [])],
                Prices = new Dictionary<string, decimal> { ["USD"] = decimal.Parse(_faker.Commerce.Price(10, 100)) }
            };

    /// <summary>
    /// Generates a fake author creation request with random data using Bogus.
    /// </summary>
    /// <returns>A CreateAuthorRequest with randomized name and biography in English and Spanish.</returns>
    public static CreateAuthorRequest GenerateFakeAuthorRequest() => new()
    {
        Id = Guid.CreateVersion7(),
        Name = _faker.Name.FullName(),
        Translations = new Dictionary<string, AuthorTranslationDto>
        {
            ["en"] = new(_faker.Lorem.Paragraphs(2)),
            ["es"] = new(_faker.Lorem.Paragraphs(2))
        }
    };

    public static BookStore.Client.UpdateAuthorRequest GenerateFakeUpdateAuthorRequest() => new()
    {
        Name = _faker.Name.FullName(),
        Translations = new Dictionary<string, AuthorTranslationDto>
        {
            ["en"] = new(_faker.Lorem.Paragraphs(2)),
            ["es"] = new(_faker.Lorem.Paragraphs(2))
        }
    };

    /// <summary>
    /// Generates a fake category creation request with random data using Bogus.
    /// </summary>
    /// <returns>A CreateCategoryRequest with randomized name and description in English and Spanish.</returns>
    public static CreateCategoryRequest GenerateFakeCategoryRequest() => new()
    {
        Id = Guid.CreateVersion7(),
        Translations = new Dictionary<string, CategoryTranslationDto>
        {
            ["en"] = new(_faker.Commerce.Department()),
            ["es"] = new(_faker.Commerce.Department())
        }
    };

    /// <summary>
    /// Generates a fake category update request with random data using Bogus.
    /// </summary>
    /// <returns>An UpdateCategoryRequest with randomized name and description in English and Spanish.</returns>
    public static BookStore.Client.UpdateCategoryRequest GenerateFakeUpdateCategoryRequest() => new()
    {
        Translations = new Dictionary<string, CategoryTranslationDto>
        {
            ["en"] = new(_faker.Commerce.Department()),
            ["es"] = new(_faker.Commerce.Department())
        }
    };

    /// <summary>
    /// Generates a fake publisher creation request with random data using Bogus.
    /// </summary>
    /// <returns>A CreatePublisherRequest with a randomized company name.</returns>
    public static CreatePublisherRequest GenerateFakePublisherRequest()
        => new() { Id = Guid.CreateVersion7(), Name = _faker.Company.CompanyName() };
}
