using BookStore.ApiService.Aggregates;
using BookStore.ApiService.Commands;
using BookStore.ApiService.Events;
using BookStore.ApiService.Handlers.Books;
using BookStore.ApiService.Infrastructure;
using BookStore.Shared.Models;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace BookStore.ApiService.UnitTests.Handlers;

/// <summary>
/// Unit tests for Book command handlers
/// Demonstrates the testability benefits of Wolverine's command/handler pattern
/// </summary>
public class BookHandlerTests
{
    static IOptions<LocalizationOptions> CreateLocalizationOptions()
        => Options.Create(new LocalizationOptions
        {
            DefaultCulture = "en",
            SupportedCultures = ["en"]
        });

    static IOptions<CurrencyOptions> CreateCurrencyOptions()
        => Options.Create(new CurrencyOptions
        {
            DefaultCurrency = "USD",
            SupportedCurrencies = ["USD", "EUR"]
        });

    [Test]
    [Category("Unit")]
    public async Task CreateBookHandler_ShouldStartStreamWithBookAddedEvent()
    {
        // Arrange
        var command = new CreateBook(
            "Clean Code",
            "978-0132350884",
            "en",
            new Dictionary<string, BookTranslationDto> // Translations
            {
                ["en"] = new BookTranslationDto("A handbook of agile software craftsmanship")
            },
            new PartialDate(2008, 8, 1),
            Guid.CreateVersion7(), // PublisherId
            [Guid.CreateVersion7()], // AuthorIds
            [Guid.CreateVersion7()], // CategoryIds
            new Dictionary<string, decimal> { ["USD"] = 10.0m } // Prices
        );

        var session = Substitute.For<IDocumentSession>();
        _ = session.CorrelationId.Returns("test-correlation-id");

        // Act
        var result = BookHandlers.Handle(command, session, CreateLocalizationOptions(), CreateCurrencyOptions(), Substitute.For<ILogger<CreateBook>>());

        // Assert
        _ = await Assert.That(result).IsNotNull();
        _ = session.Events.Received(1).StartStream<BookAggregate>(
            command.Id,
            Arg.Is<BookAdded>(e =>
                e.Title == "Clean Code" &&
                e.Isbn == "978-0132350884" &&
                e.Prices["USD"] == 10.0m));
    }

    [Test]
    [Category("Unit")]
    [Arguments("invalid", "en", 10)]
    [Arguments("en", "invalid", 10)]
    [Arguments("en", "en", 5001)]
    public async Task CreateBookHandler_WithInvalidHandlerValidation_ShouldReturnBadRequest(string language, string culture, int descLength)
    {
        // Arrange
        var description = new string('a', descLength);

        var command = new CreateBook(
            "Clean Code",
            "978-0132350884",
            language,
            new Dictionary<string, BookTranslationDto>
            {
                [culture] = new BookTranslationDto(description)
            },
            new PartialDate(2008, 8, 1),
            Guid.CreateVersion7(),
            [Guid.CreateVersion7()],
            [Guid.CreateVersion7()],
            new Dictionary<string, decimal> { ["USD"] = 10.0m }
        );

        var session = Substitute.For<IDocumentSession>();

        // Act
        var result = BookHandlers.Handle(command, session, CreateLocalizationOptions(), CreateCurrencyOptions(), Substitute.For<ILogger<CreateBook>>());

        // Assert
        _ = await Assert.That(result).IsAssignableTo<IStatusCodeHttpResult>();
        var badRequestResult = (IStatusCodeHttpResult)result;
        _ = await Assert.That(badRequestResult.StatusCode).IsEqualTo(400);
    }

    [Test]
    [Category("Unit")]
    [Arguments(0, "978-0132350884")]
    [Arguments(501, "978-0132350884")]
    [Arguments(10, "invalid-isbn")]
    public async Task CreateBookHandler_WithInvalidDomainValidation_ShouldReturnBadRequest(int titleLength, string isbn)
    {
        // Arrange
        var title = titleLength == 0 ? "" : new string('a', titleLength);

        var command = new CreateBook(
            title,
            isbn,
            "en",
            new Dictionary<string, BookTranslationDto>
            {
                ["en"] = new BookTranslationDto("Description")
            },
            new PartialDate(2008, 8, 1),
            Guid.CreateVersion7(),
            [Guid.CreateVersion7()],
            [Guid.CreateVersion7()],
            new Dictionary<string, decimal> { ["USD"] = 10.0m }
        );

        var session = Substitute.For<IDocumentSession>();

        // Act
        var result = BookHandlers.Handle(command, session, CreateLocalizationOptions(), CreateCurrencyOptions(), Substitute.For<ILogger<CreateBook>>());

        // Assert
        _ = await Assert.That(result).IsAssignableTo<IStatusCodeHttpResult>();
        var badRequestResult = (IStatusCodeHttpResult)result;
        _ = await Assert.That(badRequestResult.StatusCode).IsEqualTo(400);
    }

    [Test]
    [Category("Unit")]
    public async Task CreateBookHandler_WithMissingDefaultPrice_ShouldReturnBadRequest()
    {
        // Arrange
        var command = new CreateBook(
            "Clean Code",
            "978-0132350884",
            "en",
            new Dictionary<string, BookTranslationDto> { ["en"] = new BookTranslationDto("Description") },
            null,
            null,
            [Guid.CreateVersion7()],
            [Guid.CreateVersion7()],
            new Dictionary<string, decimal> { ["EUR"] = 10.0m } // No USD (default)
        );

        var session = Substitute.For<IDocumentSession>();

        // Act
        var result = BookHandlers.Handle(command, session, CreateLocalizationOptions(), CreateCurrencyOptions(), Substitute.For<ILogger<CreateBook>>());

        // Assert
        _ = await Assert.That(result).IsAssignableTo<IStatusCodeHttpResult>();
        var badRequestResult = (IStatusCodeHttpResult)result;
        _ = await Assert.That(badRequestResult.StatusCode).IsEqualTo(400);
    }

    [Test]
    [Category("Unit")]
    public async Task UpdateBookHandler_WithMissingBook_ShouldReturnNotFound()
    {
        // Arrange
        var command = new UpdateBook(
            Guid.CreateVersion7(),
            "Updated Title",
            null,
            "en",
            new Dictionary<string, BookTranslationDto> { ["en"] = new BookTranslationDto("Updated description") },
            null,
            null,
            [Guid.CreateVersion7()],
            [Guid.CreateVersion7()],
            new Dictionary<string, decimal> { ["USD"] = 10.0m }
        );

        var session = Substitute.For<IDocumentSession>();
        var contextAccessor = Substitute.For<IHttpContextAccessor>();
        _ = contextAccessor.HttpContext.Returns(new DefaultHttpContext());

        _ = session.Events.FetchStreamStateAsync(command.Id)
            .Returns(Task.FromResult<Marten.Events.StreamState?>(null));

        // Act
        var result = await BookHandlers.Handle(command, session, contextAccessor, CreateLocalizationOptions(), CreateCurrencyOptions(), Substitute.For<ILogger<UpdateBook>>());

        // Assert
        _ = await Assert.That(result).IsTypeOf<Microsoft.AspNetCore.Http.HttpResults.NotFound>();
    }
}
