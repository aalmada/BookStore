using System.Net;
using Bogus;
using BookStore.Client;
using Refit;
using MvcProblemDetails = Microsoft.AspNetCore.Mvc.ProblemDetails;
using SharedModels = BookStore.Shared.Models;

namespace BookStore.AppHost.Tests;

public class BookValidationTests
{
    readonly Faker _faker = new();

    [Test]
    public async Task CreateBook_WithInvalidTitle_ShouldReturnProblemDetails_WithErrorCode()
    {
        // Arrange
        var client = await TestHelpers.GetAuthenticatedClientAsync<IBooksClient>();

        // Manual request construction with invalid title
        var request = new CreateBookRequest
        {
            Title = "", // Invalid
            Isbn = _faker.Commerce.Ean13(),
            Language = "en",
            Translations =
                new Dictionary<string, BookTranslationDto>
                {
                    ["en"] = new() { Description = _faker.Lorem.Paragraph() }
                },
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

            var problemDetails = await ex.GetContentAsAsync<MvcProblemDetails>();
            _ = await Assert.That(problemDetails).IsNotNull();
            _ = await Assert.That(problemDetails!.Status).IsEqualTo(400);
            _ = await Assert.That(problemDetails.Title).IsEqualTo("Bad Request");

            // Check content for TitleRequired. 
            // If another error occurs first, this might fail, but checking presence is good.
            _ = await Assert.That(ex.Content).Contains(SharedModels.ErrorCodes.Books.TitleRequired);
        }
    }

    [Test]
    public async Task CreateBook_WithInvalidLanguage_ShouldReturnProblemDetails_WithErrorCode()
    {
        // Arrange
        var client = await TestHelpers.GetAuthenticatedClientAsync<IBooksClient>();

        var request = new CreateBookRequest
        {
            Title = _faker.Commerce.ProductName(),
            Isbn = _faker.Commerce.Ean13(),
            Language = "invalid-lang", // Invalid
            Translations =
                new Dictionary<string, BookTranslationDto>
                {
                    ["en"] = new() { Description = _faker.Lorem.Paragraph() }
                },
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
            _ = await Assert.That(ex.Content).Contains(SharedModels.ErrorCodes.Books.LanguageInvalid);
        }
    }
}
