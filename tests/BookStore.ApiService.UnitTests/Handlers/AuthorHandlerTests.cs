using BookStore.ApiService.Aggregates;
using BookStore.ApiService.Commands;
using BookStore.ApiService.Events;
using BookStore.ApiService.Handlers.Authors;
using Microsoft.AspNetCore.Http;

namespace BookStore.ApiService.UnitTests.Handlers;

public class AuthorHandlerTests : HandlerTestBase
{
    [Test]
    [Category("Unit")]
    public async Task CreateAuthorHandler_ShouldStartStreamWithAuthorAddedEvent()
    {
        // Arrange
        var command = new CreateAuthor(
            Guid.CreateVersion7(),
            "Robert C. Martin",
            new Dictionary<string, AuthorTranslationDto> { ["en"] = new AuthorTranslationDto("Uncle Bob") }
        );

        // Act
        var result = await AuthorHandlers.Handle(command, Session, LocalizationOptions, Cache, Logger);

        // Assert
        _ = await Assert.That(result).IsNotNull();
        _ = Session.Events.Received(1).StartStream<AuthorAggregate>(
            command.Id,
            Arg.Is<AuthorAdded>((AuthorAdded e) =>
                e.Name == "Robert C. Martin"));
    }

    [Test]
    [Category("Unit")]
    [Arguments("invalid-culture")]
    [Arguments("xx-XX")]
    [Arguments("123")]
    public async Task CreateAuthorHandler_WithInvalidCulture_ShouldReturnBadRequest(string invalidCulture)
    {
        // Arrange
        var command = new CreateAuthor(
            Guid.CreateVersion7(),
            "Robert C. Martin",
            new Dictionary<string, AuthorTranslationDto> { [invalidCulture] = new AuthorTranslationDto("Uncle Bob") }
        );

        // Act
        var result = await AuthorHandlers.Handle(command, Session, LocalizationOptions, Cache, Logger);

        // Assert
        _ = await Assert.That(result).IsAssignableTo<IStatusCodeHttpResult>();
        var badRequestResult = (IStatusCodeHttpResult)result;
        _ = await Assert.That(badRequestResult.StatusCode).IsEqualTo(400);
    }

    [Test]
    [Category("Unit")]
    public async Task UpdateAuthorHandler_ShouldAppendAuthorUpdatedEvent()
    {
        // Arrange
        var command = new UpdateAuthor(
            Guid.CreateVersion7(),
            "Robert C. Martin Updated",
            new Dictionary<string, AuthorTranslationDto> { ["en"] = new AuthorTranslationDto("Uncle Bob Updated") }
        ) { ETag = "test-etag" };

        // Mock Stream State
        _ = Session.Events.FetchStreamStateAsync(command.Id).Returns(new Marten.Events.StreamState { Version = 1 });

        // Mock Aggregate Load
        var existingAggregate = AggregateFactory.Hydrate<AuthorAggregate>(
            new AuthorAdded(command.Id, "Old Name", new Dictionary<string, AuthorTranslation> { ["en"] = new("Bio") },
                DateTimeOffset.UtcNow));
        _ = Session.Events.AggregateStreamAsync<AuthorAggregate>(command.Id).Returns(existingAggregate);

        // Act
        var result =
            await AuthorHandlers.Handle(command, Session, HttpContextAccessor, LocalizationOptions, Cache, Logger,
                CancellationToken.None);

        // Assert
        _ = await Assert.That(result).IsTypeOf<Microsoft.AspNetCore.Http.HttpResults.NoContent>();
        _ = Session.Events.Received(1).Append(
            command.Id,
            Arg.Is<AuthorUpdated>((AuthorUpdated e) =>
                e.Name == "Robert C. Martin Updated"));
    }

    [Test]
    [Category("Unit")]
    public async Task SoftDeleteAuthorHandler_ShouldAppendAuthorSoftDeletedEvent()
    {
        // Arrange
        var id = Guid.CreateVersion7();
        var command = new SoftDeleteAuthor(id);

        // Mock Stream State
        _ = Session.Events.FetchStreamStateAsync(id).Returns(new Marten.Events.StreamState { Version = 1 });

        // Mock Aggregate Load
        var existingAggregate = AggregateFactory.Hydrate<AuthorAggregate>(
            new AuthorAdded(id, "Author", new Dictionary<string, AuthorTranslation> { ["en"] = new("Bio") },
                DateTimeOffset.UtcNow));
        _ = Session.Events.AggregateStreamAsync<AuthorAggregate>(id).Returns(existingAggregate);

        // Act
        var result = await AuthorHandlers.Handle(command, Session, HttpContextAccessor, Cache, Logger,
            CancellationToken.None);

        // Assert
        _ = await Assert.That(result).IsTypeOf<Microsoft.AspNetCore.Http.HttpResults.NoContent>();
        _ = Session.Events.Received(1).Append(
            id,
            Arg.Is<AuthorSoftDeleted>(e => e.Id == id));
    }

    [Test]
    [Category("Unit")]
    public async Task RestoreAuthorHandler_ShouldAppendAuthorRestoredEvent()
    {
        // Arrange
        var id = Guid.CreateVersion7();
        var command = new RestoreAuthor(id);

        // Mock Stream State
        _ = Session.Events.FetchStreamStateAsync(id).Returns(new Marten.Events.StreamState { Version = 1 });

        // Mock Aggregate Load - Create DELETED aggregate
        var existingAggregate = AggregateFactory.Hydrate<AuthorAggregate>(
            new AuthorAdded(id, "Author", new Dictionary<string, AuthorTranslation> { ["en"] = new("Bio") },
                DateTimeOffset.UtcNow),
            new AuthorSoftDeleted(id, DateTimeOffset.UtcNow));
        _ = Session.Events.AggregateStreamAsync<AuthorAggregate>(id).Returns(existingAggregate);

        // Act
        var result = await AuthorHandlers.Handle(command, Session, HttpContextAccessor, Cache, Logger,
            CancellationToken.None);

        // Assert
        _ = await Assert.That(result).IsTypeOf<Microsoft.AspNetCore.Http.HttpResults.NoContent>();
        _ = Session.Events.Received(1).Append(
            id,
            Arg.Is<AuthorRestored>(e => e.Id == id));
    }
}
