using BookStore.ApiService.Events;
using BookStore.ApiService.Projections;
using BookStore.Shared.Models;
using Marten;
using NSubstitute;

namespace BookStore.ApiService.UnitTests.Projections;

public class PublisherStatisticsProjectionTests
{
    readonly PublisherStatisticsProjectionBuilder _projection = new();


    static PublisherStatistics CreateState(Guid publisherId, int count, Guid? includeBookId = null)
    {
        var stats = new PublisherStatistics
        {
            Id = publisherId,
            BookCount = count
        };
        
        if (includeBookId.HasValue)
        {
            stats.BookIds.Add(includeBookId.Value);
        }
        
        while (stats.BookIds.Count < count)
        {
            stats.BookIds.Add(Guid.CreateVersion7());
        }
        
        return stats;
    }

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
        _projection.Apply(publisherId, @event, state);

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
        _projection.Apply(publisherId, @event, state);

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
        _projection.Apply(publisherId, @event, state);

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
        var state = CreateState(publisherId, 5, bookId); // Ensure bookId is in the set

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
        _projection.Apply(publisherId, @event, state);

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
        var state = CreateState(publisherId, 5, bookId); // Ensure bookId is in the set


        var @event = new BookSoftDeleted(bookId, DateTimeOffset.UtcNow);

        // Act
        _projection.Apply(publisherId, @event, state);

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


        var @event = new BookRestored(bookId, DateTimeOffset.UtcNow);

        // Act
        _projection.Apply(publisherId, @event, state);

        // Assert
        _ = await Assert.That(state.BookCount).IsEqualTo(6);
    }
}

