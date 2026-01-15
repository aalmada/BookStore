using BookStore.ApiService.Events;
using BookStore.ApiService.Projections;
using BookStore.Shared.Models;
using Marten;
using NSubstitute;

namespace BookStore.ApiService.UnitTests.Projections;

public class PublisherStatisticsProjectionTests
{
    readonly PublisherStatisticsProjectionBuilder _projection = new();
    readonly IQuerySession _session = Substitute.For<IQuerySession>();

    static PublisherStatistics CreateState(Guid publisherId, int count) => new()
    {
        Id = publisherId,
        BookCount = count
    };

    [Test]
    [Category("Unit")]
    public async Task Create_ShouldInitializeCountToZero()
    {
        // Arrange
        var publisherId = Guid.CreateVersion7();
        var @event = new PublisherAdded(
            publisherId,
            "Test Publisher",
            DateTimeOffset.UtcNow);

        // Act
        var result = _projection.Create(@event);

        // Assert
        _ = await Assert.That(result.Id).IsEqualTo(publisherId);
        _ = await Assert.That(result.BookCount).IsEqualTo(0);
    }

    [Test]
    [Category("Unit")]
    public async Task Apply_BookAdded_WhenPublisherMatches_ShouldIncrement()
    {
        // Arrange
        var publisherId = Guid.CreateVersion7();
        var state = CreateState(publisherId, 0);

        var @event = new BookAdded(
            Guid.CreateVersion7(),
            "Title",
            "isbn",
            "en",
            [],
            null,
            publisherId, // Matches publisher
            [],
            [],
            []);

        // Act
        await _projection.Apply(@event, state, _session);

        // Assert
        _ = await Assert.That(state.BookCount).IsEqualTo(1);
    }

    [Test]
    [Category("Unit")]
    public async Task Apply_BookAdded_WhenPublisherDoesNotMatch_ShouldNotIncrement()
    {
        // Arrange
        var publisherId = Guid.CreateVersion7();
        var state = CreateState(publisherId, 0);

        var @event = new BookAdded(
            Guid.CreateVersion7(),
            "Title",
            "isbn",
            "en",
            [],
            null,
            Guid.CreateVersion7(), // Different publisher
            [],
            [],
            []);

        // Act
        await _projection.Apply(@event, state, _session);

        // Assert
        _ = await Assert.That(state.BookCount).IsEqualTo(0);
    }

    [Test]
    [Category("Unit")]
    public async Task Apply_BookUpdated_WhenPublisherChangedToTarget_ShouldIncrement()
    {
        // Arrange
        var publisherId = Guid.CreateVersion7();
        var bookId = Guid.CreateVersion7();
        var state = CreateState(publisherId, 5);

        // Previous state of book: Different publisher
        var previousBookState = new BookSearchProjection
        {
            Id = bookId,
            PublisherId = Guid.CreateVersion7()
        };
        _ = _session.LoadAsync<BookSearchProjection>(bookId).Returns(previousBookState);

        // Update event: Changed TO this publisher
        var @event = new BookUpdated(
            bookId,
            "Title",
            "isbn",
            "en",
            [],
            null,
            publisherId, // Changed to this
            [],
            [],
            []);

        // Act
        await _projection.Apply(@event, state, _session);

        // Assert
        _ = await Assert.That(state.BookCount).IsEqualTo(6);
    }

    [Test]
    [Category("Unit")]
    public async Task Apply_BookUpdated_WhenPublisherChangedFromTarget_ShouldDecrement()
    {
        // Arrange
        var publisherId = Guid.CreateVersion7();
        var bookId = Guid.CreateVersion7();
        var state = CreateState(publisherId, 5);

        // Previous state of book: Was this publisher
        var previousBookState = new BookSearchProjection
        {
            Id = bookId,
            PublisherId = publisherId
        };
        _ = _session.LoadAsync<BookSearchProjection>(bookId).Returns(previousBookState);

        // Update event: Changed FROM this publisher
        var @event = new BookUpdated(
            bookId,
            "Title",
            "isbn",
            "en",
            [],
            null,
            Guid.CreateVersion7(), // Changed away
            [],
            [],
            []);

        // Act
        await _projection.Apply(@event, state, _session);

        // Assert
        _ = await Assert.That(state.BookCount).IsEqualTo(4);
    }

    [Test]
    [Category("Unit")]
    public async Task Apply_BookSoftDeleted_WhenPublisherMatches_ShouldDecrement()
    {
        // Arrange
        var publisherId = Guid.CreateVersion7();
        var bookId = Guid.CreateVersion7();
        var state = CreateState(publisherId, 5);

        var previousBookState = new BookSearchProjection
        {
            Id = bookId,
            PublisherId = publisherId
        };
        _ = _session.LoadAsync<BookSearchProjection>(bookId).Returns(previousBookState);

        var @event = new BookSoftDeleted(bookId, DateTimeOffset.UtcNow);

        // Act
        await _projection.Apply(@event, state, _session);

        // Assert
        _ = await Assert.That(state.BookCount).IsEqualTo(4);
    }

    [Test]
    [Category("Unit")]
    public async Task Apply_BookRestored_WhenPublisherMatches_ShouldIncrement()
    {
        // Arrange
        var publisherId = Guid.CreateVersion7();
        var bookId = Guid.CreateVersion7();
        var state = CreateState(publisherId, 5);

        var previousBookState = new BookSearchProjection
        {
            Id = bookId,
            PublisherId = publisherId
        };
        _ = _session.LoadAsync<BookSearchProjection>(bookId).Returns(previousBookState);

        var @event = new BookRestored(bookId, DateTimeOffset.UtcNow);

        // Act
        await _projection.Apply(@event, state, _session);

        // Assert
        _ = await Assert.That(state.BookCount).IsEqualTo(6);
    }
}
