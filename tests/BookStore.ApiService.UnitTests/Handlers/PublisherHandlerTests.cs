using BookStore.ApiService.Aggregates;
using BookStore.ApiService.Commands;
using BookStore.ApiService.Events;
using BookStore.ApiService.Handlers.Publishers;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;

namespace BookStore.ApiService.UnitTests.Handlers;

public class PublisherHandlerTests : HandlerTestBase
{
    [Test]
    [Category("Unit")]
    public async Task CreatePublisherHandler_ShouldStartStreamWithPublisherAddedEvent()
    {
        // Arrange
        var command = new CreatePublisher(Guid.CreateVersion7(), "O'Reilly Media");

        // Act
        var result = await PublisherHandlers.Handle(command, Session, Cache, Logger);

        // Assert
        _ = await Assert.That(result).IsNotNull();
        _ = Session.Events.Received(1).StartStream<PublisherAggregate>(
            command.Id,
            Arg.Is<PublisherAdded>(e =>
                e.Name == "O'Reilly Media"));
    }

    [Test]
    [Category("Unit")]
    public async Task UpdatePublisherHandler_ShouldAppendPublisherUpdatedEvent()
    {
        // Arrange
        var command = new UpdatePublisher(Guid.CreateVersion7(), "O'Reilly Media Updated") { ETag = "test-etag" };

        // Mock Stream State
        _ = Session.Events.FetchStreamStateAsync(command.Id).Returns(new Marten.Events.StreamState { Version = 1 });

        // Mock Aggregate Load
        var existingAggregate = AggregateFactory.Hydrate<PublisherAggregate>(
            new PublisherAdded(command.Id, "Old Name", DateTimeOffset.UtcNow));
        _ = Session.Events.AggregateStreamAsync<PublisherAggregate>(command.Id).Returns(existingAggregate);

        // Act
        var result = await PublisherHandlers.Handle(command, Session, Cache, Logger);

        // Assert
        _ = await Assert.That(result).IsTypeOf<NoContent>();
        _ = Session.Events.Received(1).Append(
            command.Id,
            Arg.Is<PublisherUpdated>(e =>
                e.Name == "O'Reilly Media Updated"));
    }

    [Test]
    [Category("Unit")]
    public async Task SoftDeletePublisherHandler_ShouldAppendPublisherSoftDeletedEvent()
    {
        // Arrange
        var id = Guid.CreateVersion7();
        var command = new SoftDeletePublisher(id);

        // Mock Stream State
        _ = Session.Events.FetchStreamStateAsync(id).Returns(new Marten.Events.StreamState { Version = 1 });

        // Mock Aggregate Load
        var existingAggregate = AggregateFactory.Hydrate<PublisherAggregate>(
            new PublisherAdded(id, "O'Reilly", DateTimeOffset.UtcNow));
        _ = Session.Events.AggregateStreamAsync<PublisherAggregate>(id).Returns(existingAggregate);

        // Act
        var result = await PublisherHandlers.Handle(command, Session, Cache, Logger);

        // Assert
        _ = await Assert.That(result).IsTypeOf<NoContent>();
        _ = Session.Events.Received(1).Append(
            id,
            Arg.Is<PublisherSoftDeleted>(e => e.Id == id));
    }

    [Test]
    [Category("Unit")]
    public async Task RestorePublisherHandler_ShouldAppendPublisherRestoredEvent()
    {
        // Arrange
        var id = Guid.CreateVersion7();
        var command = new RestorePublisher(id);

        // Mock Stream State
        _ = Session.Events.FetchStreamStateAsync(id).Returns(new Marten.Events.StreamState { Version = 1 });

        // Mock Aggregate Load - Create DELETED aggregate
        var existingAggregate = AggregateFactory.Hydrate<PublisherAggregate>(
            new PublisherAdded(id, "O'Reilly", DateTimeOffset.UtcNow),
            new PublisherSoftDeleted(id, DateTimeOffset.UtcNow));
        _ = Session.Events.AggregateStreamAsync<PublisherAggregate>(id).Returns(existingAggregate);

        // Act
        var result = await PublisherHandlers.Handle(command, Session, Cache, Logger);

        // Assert
        _ = await Assert.That(result).IsTypeOf<NoContent>();
        _ = Session.Events.Received(1).Append(
            id,
            Arg.Is<PublisherRestored>(e => e.Id == id));
    }
}
