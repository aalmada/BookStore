using BookStore.ApiService.Aggregates;
using BookStore.ApiService.Commands;
using BookStore.ApiService.Events;
using BookStore.ApiService.Handlers.Books;
using BookStore.ApiService.Handlers.Sales;
using BookStore.ApiService.Infrastructure;
using JasperFx.Events;
using Marten.Events;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;

namespace BookStore.ApiService.UnitTests.Handlers;

/// <summary>
/// Unit tests for Book command handlers
/// Demonstrates the testability benefits of Wolverine's command/handler pattern
/// </summary>
public class BookHandlerTests : HandlerTestBase
{
    [Test]
    [Category("Unit")]
    public async Task CreateBookHandler_ShouldStartStreamWithBookAddedEvent()
    {
        // Arrange
        var command = new CreateBook(
            Guid.CreateVersion7(),
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

        // Act
        var result = await BookHandlers.Handle(command, Session, LocalizationOptions, CurrencyOptions, Cache, Logger);

        // Assert
        _ = await Assert.That(result).IsNotNull();
        _ = Session.Events.Received(1).StartStream<BookAggregate>(
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
    public async Task CreateBookHandler_WithInvalidHandlerValidation_ShouldReturnBadRequest(string language,
        string culture, int descLength)
    {
        // Arrange
        var description = new string('a', descLength);

        var command = new CreateBook(
            Guid.CreateVersion7(),
            "Clean Code",
            "978-0132350884",
            language,
            new Dictionary<string, BookTranslationDto> { [culture] = new BookTranslationDto(description) },
            new PartialDate(2008, 8, 1),
            Guid.CreateVersion7(),
            [Guid.CreateVersion7()],
            [Guid.CreateVersion7()],
            new Dictionary<string, decimal> { ["USD"] = 10.0m }
        );

        // Act
        var result = await BookHandlers.Handle(command, Session, LocalizationOptions, CurrencyOptions, Cache, Logger);

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
            Guid.CreateVersion7(),
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

        // Act
        var result = await BookHandlers.Handle(command, Session, LocalizationOptions, CurrencyOptions, Cache, Logger);

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

        _ = Session.Events.FetchStreamStateAsync(command.Id)
            .Returns(Task.FromResult<Marten.Events.StreamState?>(null));

        // Act
        var result = await BookHandlers.Handle(command, Session, HttpContextAccessor, LocalizationOptions,
            CurrencyOptions, Cache, Logger);

        // Assert
        _ = await Assert.That(result).IsTypeOf<NotFound>();
    }

    [Test]
    [Category("Unit")]
    public async Task ScheduleSale_ShouldAppendEvent()
    {
        // Arrange
        var bookId = Guid.CreateVersion7();
        var command = new ScheduleSale(bookId, 10m, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1));

        // SaleHandlers uses FetchStreamAsync and projects manually
        // Provide at least one event so it doesn't return NotFound
        var events = new List<IEvent>
        {
            new JasperFx.Events.Event<BookAdded>(new BookAdded(bookId, "Title", "ISBN", "en", [], null, null, [],
                [], [])) { Version = 1 }
        };
        _ = Session.Events.FetchStreamAsync(bookId).Returns(events);

        // Act
        var result = await SaleHandlers.Handle(command, Session, Cache);

        // Assert
        _ = await Assert.That(result).IsTypeOf<NoContent>();
        // Handler calls unversioned Append(id, event) when no ETag is provided
        _ = Session.Events.Received(1).Append(
            bookId,
            Arg.Is<BookSaleScheduled>(e => e.Sale.Percentage == 10m));
    }

    [Test]
    [Category("Unit")]
    public async Task CancelSale_ShouldAppendEvent()
    {
        // Arrange
        var bookId = Guid.CreateVersion7();
        var saleStart = DateTimeOffset.UtcNow;
        var command = new CancelSale(bookId, saleStart);

        // SaleHandlers.Handle for CancelSale fetches stream and projects manually
        // Provide BookAdded and then BookSaleScheduled
        var events = new List<IEvent>
        {
            new JasperFx.Events.Event<BookAdded>(new BookAdded(bookId, "Title", "ISBN", "en", [], null, null, [], [],
                [])) { Version = 1 },
            new JasperFx.Events.Event<BookSaleScheduled>(new BookSaleScheduled(bookId,
                new BookSale(10m, saleStart, saleStart.AddDays(1)))) { Version = 2 }
        };
        _ = Session.Events.FetchStreamAsync(bookId).Returns(events);

        // Act
        var result = await SaleHandlers.Handle(command, Session, Cache);

        // Assert
        _ = await Assert.That(result).IsTypeOf<NoContent>();
        _ = Session.Events.Received(1).Append(
            bookId,
            Arg.Is<BookSaleCancelled>(e => e.SaleStart == saleStart));
    }
}
