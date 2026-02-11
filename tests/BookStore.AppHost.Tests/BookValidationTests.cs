using System.Net;
using Bogus;
using BookStore.Client;
using BookStore.Shared.Models;
using Refit;
using MvcProblemDetails = Microsoft.AspNetCore.Mvc.ProblemDetails;
using SharedModels = BookStore.Shared.Models;

namespace BookStore.AppHost.Tests;

public class BookValidationTests
{
    readonly Faker _faker = new();

    [Test]
    [Arguments("", "en", SharedModels.ErrorCodes.Books.TitleRequired)]
    [Arguments("Valid Title", "invalid-lang", SharedModels.ErrorCodes.Books.LanguageInvalid)]
    public async Task CreateBook_WithInvalidData_ShouldReturnProblemDetails_WithErrorCode(
        string title,
        string language,
        string expectedErrorCode)
    {
        // Arrange
        var client = await TestHelpers.GetAuthenticatedClientAsync<IBooksClient>();

        var request = new CreateBookRequest
        {
            Id = Guid.CreateVersion7(),
            Title = title,
            Isbn = _faker.Commerce.Ean13(),
            Language = language,
            Translations =
                new Dictionary<string, BookTranslationDto> { ["en"] = new(_faker.Lorem.Paragraph()) },
            PublicationDate = new SharedModels.PartialDate(2023),
            AuthorIds = [],
            CategoryIds = [],
            Prices = new Dictionary<string, decimal> { ["USD"] = 10.99m }
        };

        // Act & Assert
        try
        {
            _ = await client.CreateBookAsync(request);
            Assert.Fail("Should have thrown ApiException");
        }
        catch (ApiException ex)
        {
            _ = await Assert.That(ex.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
            _ = await Assert.That(ex.Content).Contains(expectedErrorCode);
        }
    }
}
