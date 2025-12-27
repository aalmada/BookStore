using BookStore.ApiService.Aggregates;
using BookStore.ApiService.Commands.Books;
using BookStore.ApiService.Events;
using BookStore.ApiService.Handlers.Books;
using Marten;
using Microsoft.AspNetCore.Http;
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
            "A Handbook of Agile Software Craftsmanship",
            new DateOnly(2008, 8, 1),
            Guid.CreateVersion7(),
            [Guid.CreateVersion7()],
            [Guid.CreateVersion7()]);
        
        var session = Substitute.For<IDocumentSession>();
        session.CorrelationId.Returns("test-correlation-id");
        
        // Act
        var result = BookHandlers.Handle(command, session);
        
        // Assert
        await Assert.That(result).IsNotNull();
        session.Events.Received(1).StartStream<BookAggregate>(
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
            null,
            null,
            null,
            [],
            []);
        
        var session = Substitute.For<IDocumentSession>();
        var context = new DefaultHttpContext();
        
        // Stream doesn't exist
        session.Events.FetchStreamStateAsync(command.Id)
            .Returns(Task.FromResult<Marten.Events.StreamState?>(null));
        
        // Act
        var result = await BookHandlers.Handle(command, session, context);
        
        // Assert
        await Assert.That(result).IsTypeOf<Microsoft.AspNetCore.Http.HttpResults.NotFound>();
    }
}
