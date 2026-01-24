using System.Net;
using System.Net.Http.Json;
using Bogus;
using BookStore.Shared.Models;
using Microsoft.AspNetCore.Mvc;

namespace BookStore.AppHost.Tests;

public class BookValidationTests
{
    readonly Faker _faker = new();

    [Test]
    public async Task CreateBook_WithInvalidTitle_ShouldReturnProblemDetails_WithErrorCode()
    {
        // Arrange
        var client = await TestHelpers.GetAuthenticatedClientAsync();

        // Manual request construction with invalid title
        var request = new
        {
            Title = "", // Invalid
            Isbn = _faker.Commerce.Ean13(),
            Language = "en",
            Translations = new Dictionary<string, object> { ["en"] = new { Description = _faker.Lorem.Paragraph() } },
            PublicationDate = new { Year = 2023, Month = 1, Day = 1 },
            Prices = new Dictionary<string, decimal> { ["USD"] = 10.99m }
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/admin/books", request);

        // Assert
        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);

        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        _ = await Assert.That(problemDetails).IsNotNull();
        _ = await Assert.That(problemDetails!.Status).IsEqualTo(400);
        _ = await Assert.That(problemDetails.Title).IsEqualTo("Bad Request");

        var json = await response.Content.ReadAsStringAsync();
        _ = await Assert.That(json).Contains(ErrorCodes.Books.TitleRequired);
    }

    [Test]
    public async Task CreateBook_WithInvalidLanguage_ShouldReturnProblemDetails_WithErrorCode()
    {
        // Arrange
        var client = await TestHelpers.GetAuthenticatedClientAsync();

        var request = new
        {
            Title = _faker.Commerce.ProductName(),
            Isbn = _faker.Commerce.Ean13(),
            Language = "invalid-lang", // Invalid
            Translations = new Dictionary<string, object> { ["en"] = new { Description = _faker.Lorem.Paragraph() } },
            PublicationDate = new { Year = 2023, Month = 1, Day = 1 },
            Prices = new Dictionary<string, decimal> { ["USD"] = 10.99m }
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/admin/books", request);

        // Assert
        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadAsStringAsync();
        _ = await Assert.That(json).Contains(ErrorCodes.Books.LanguageInvalid);
    }
}
