using BookStore.ApiService.Aggregates;
using BookStore.ApiService.Commands;
using BookStore.ApiService.Events;
using BookStore.ApiService.Handlers.Books;
using BookStore.ApiService.Infrastructure;
using BookStore.ApiService.Models;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace BookStore.ApiService.Tests.Handlers;

/// <summary>
/// Unit tests for Book command handlers
/// Demonstrates the testability benefits of Wolverine's command/handler pattern
/// </summary>
public class BookHandlerTests
{
    [Test]
    [Category("Unit")]
    public async Task CreateBookHandler_ShouldStartStreamWithBookAddedEvent()
    {
        // Arrange
        var command = new CreateBook(
            "Clean Code",
            "978-0132350884",
            "en",
            [], // Translations
            new PartialDate(2008, 8, 1),
            Guid.CreateVersion7(), // PublisherId
            [Guid.CreateVersion7()], // AuthorIds
            [Guid.CreateVersion7()]  // CategoryIds
        );

        var session = Substitute.For<IDocumentSession>();
        _ = session.CorrelationId.Returns("test-correlation-id");

        // Act
        var localizationOptions = Options.Create(new LocalizationOptions { DefaultCulture = "en" });
        var (result, notification) = BookHandlers.Handle(command, session, localizationOptions);

        // Assert
        _ = await Assert.That(result).IsNotNull();
        _ = session.Events.Received(1).StartStream<BookAggregate>(
            command.Id,
            Arg.Is<BookAdded>(e =>
                e.Title == "Clean Code" &&
                e.Isbn == "978-0132350884"));
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
            "en", // Language (non-nullable)
            null, // Translations
            null, // PartialDate
            null, // PublisherId
            [],   // AuthorIds
            []    // CategoryIds
        );

        var session = Substitute.For<IDocumentSession>();
        var context = new DefaultHttpContext();

        // Stream doesn't exist
        _ = session.Events.FetchStreamStateAsync(command.Id)
            .Returns(Task.FromResult<Marten.Events.StreamState?>(null));

        // Act
        var localizationOptions = Options.Create(new LocalizationOptions { DefaultCulture = "en" });
        var result = await BookHandlers.Handle(command, session, context, localizationOptions);

        // Assert
        _ = await Assert.That(result).IsTypeOf<Microsoft.AspNetCore.Http.HttpResults.NotFound>();
    }
}
