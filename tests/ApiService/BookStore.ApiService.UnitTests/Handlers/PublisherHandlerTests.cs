using BookStore.ApiService.Aggregates;
using BookStore.ApiService.Commands;
using BookStore.ApiService.Events;
using BookStore.ApiService.Handlers.Publishers;
using BookStore.ApiService.Infrastructure;
using BookStore.Shared.Models;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace BookStore.ApiService.UnitTests.Handlers;

public class PublisherHandlerTests
{
    [Test]
    [Category("Unit")]
    public async Task CreatePublisherHandler_ShouldStartStreamWithPublisherAddedEvent()
    {
        // Arrange
        var command = new CreatePublisher("O'Reilly Media");

        var session = Substitute.For<IDocumentSession>();
        _ = session.CorrelationId.Returns("test-correlation-id");

        // Act
        var result = PublisherHandlers.Handle(command, session, Substitute.For<ILogger<CreatePublisher>>());

        // Assert
        _ = await Assert.That(result).IsNotNull();
        _ = session.Events.Received(1).StartStream<PublisherAggregate>(
            command.Id,
            Arg.Is<PublisherAdded>(e =>
                e.Name == "O'Reilly Media"));
    }

    [Test]
    [Category("Unit")]
    public async Task UpdatePublisherHandler_ShouldAppendPublisherUpdatedEvent()
    {
        // Arrange
        var command = new UpdatePublisher(
            Guid.CreateVersion7(),
            "O'Reilly Updated"
        )
        {
            ETag = "test-etag"
        };

        var session = Substitute.For<IDocumentSession>();
        var httpContext = new DefaultHttpContext();
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        _ = httpContextAccessor.HttpContext.Returns(httpContext);

        // Mock Stream State
        _ = session.Events.FetchStreamStateAsync(command.Id).Returns(new Marten.Events.StreamState { Version = 1 });

        // Mock Aggregate Load
        var existingAggregate = CreatePublisherAggregate(command.Id, "O'Reilly", false);
        _ = session.Events.AggregateStreamAsync<PublisherAggregate>(command.Id).Returns(existingAggregate);

        // Act
        var result = await PublisherHandlers.Handle(command, session, httpContextAccessor, Substitute.For<ILogger<UpdatePublisher>>());

        // Assert
        _ = await Assert.That(result).IsTypeOf<Microsoft.AspNetCore.Http.HttpResults.NoContent>();
        _ = session.Events.Received(1).Append(
            command.Id,
            Arg.Is<PublisherUpdated>(e =>
                e.Name == "O'Reilly Updated"));
    }

    [Test]
    [Category("Unit")]
    public async Task SoftDeletePublisherHandler_ShouldAppendPublisherSoftDeletedEvent()
    {
        // Arrange
        var id = Guid.CreateVersion7();
        var command = new SoftDeletePublisher(id);

        var session = Substitute.For<IDocumentSession>();
        var httpContext = new DefaultHttpContext();
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        _ = httpContextAccessor.HttpContext.Returns(httpContext);

        // Mock Stream State
        _ = session.Events.FetchStreamStateAsync(id).Returns(new Marten.Events.StreamState { Version = 1 });

        // Mock Aggregate Load
        var existingAggregate = CreatePublisherAggregate(id, "O'Reilly", false);
        _ = session.Events.AggregateStreamAsync<PublisherAggregate>(id).Returns(existingAggregate);

        // Act
        var result = await PublisherHandlers.Handle(command, session, httpContextAccessor, Substitute.For<ILogger<SoftDeletePublisher>>());

        // Assert
        _ = await Assert.That(result).IsTypeOf<Microsoft.AspNetCore.Http.HttpResults.NoContent>();
        _ = session.Events.Received(1).Append(
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

        var session = Substitute.For<IDocumentSession>();
        var httpContext = new DefaultHttpContext();
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        _ = httpContextAccessor.HttpContext.Returns(httpContext);

        // Mock Stream State
        _ = session.Events.FetchStreamStateAsync(id).Returns(new Marten.Events.StreamState { Version = 1 });

        // Mock Aggregate Load - Create DELETED aggregate
        var existingAggregate = CreatePublisherAggregate(id, "O'Reilly", true);
        _ = session.Events.AggregateStreamAsync<PublisherAggregate>(id).Returns(existingAggregate);

        // Act
        var result = await PublisherHandlers.Handle(command, session, httpContextAccessor, Substitute.For<ILogger<RestorePublisher>>());

        // Assert
        _ = await Assert.That(result).IsTypeOf<Microsoft.AspNetCore.Http.HttpResults.NoContent>();
        _ = session.Events.Received(1).Append(
            id,
            Arg.Is<PublisherRestored>(e => e.Id == id));
    }

    static PublisherAggregate CreatePublisherAggregate(Guid id, string name, bool isDeleted)
    {
        var aggregate = (PublisherAggregate)Activator.CreateInstance(typeof(PublisherAggregate), true)!;

        typeof(PublisherAggregate).GetProperty(nameof(PublisherAggregate.Id))!.SetValue(aggregate, id);
        typeof(PublisherAggregate).GetProperty(nameof(PublisherAggregate.Name))!.SetValue(aggregate, name);
        typeof(PublisherAggregate).GetProperty(nameof(PublisherAggregate.Deleted))!.SetValue(aggregate, isDeleted);

        return aggregate;
    }
}
