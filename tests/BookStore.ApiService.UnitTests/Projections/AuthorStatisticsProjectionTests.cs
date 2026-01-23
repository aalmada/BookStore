using BookStore.ApiService.Events;
using BookStore.ApiService.Projections;
using BookStore.Shared.Models;
using Marten;
using NSubstitute;

namespace BookStore.ApiService.UnitTests.Projections;

public class AuthorStatisticsProjectionTests
{
    readonly AuthorStatisticsProjectionBuilder _projection = new();

    // Helper to create a projection state
    static AuthorStatistics CreateState(Guid authorId, int count, Guid? includeBookId = null)
    {
        var stats = new AuthorStatistics
        {
            Id = authorId,
            BookCount = count
        };
        
        if (includeBookId.HasValue)
        {
            _ = stats.BookIds.Add(includeBookId.Value);
        }
        
        while (stats.BookIds.Count < count)
        {
            _ = stats.BookIds.Add(Guid.CreateVersion7());
        }
        
        return stats;
    }

    [Test]
    [Category("Unit")]
    public async Task Create_ShouldInitializeCountToZero()
    {
        // Arrange
        var authorId = Guid.CreateVersion7();
        var @event = new AuthorAdded(
            authorId,
            "Test Author",
            [],
            DateTimeOffset.UtcNow);

        // Act
        var result = _projection.Create(@event);

        // Assert
        _ = await Assert.That(result.Id).IsEqualTo(authorId);
        _ = await Assert.That(result.BookCount).IsEqualTo(0);
    }

    [Test]
    [Category("Unit")]
    public async Task Apply_BookAdded_WhenAuthorInBook_ShouldIncrement()
    {
        // Arrange
        var authorId = Guid.CreateVersion7();
        var state = CreateState(authorId, 0);

        var @event = new BookAdded(
            Guid.CreateVersion7(),
            "Title",
            "isbn",
            "en",
            [],
            null,
            Guid.CreateVersion7(),
            [authorId], // Author is included
            [],
            []);

        // Act
        _projection.Apply(authorId, @event, state);

        // Assert
        _ = await Assert.That(state.BookCount).IsEqualTo(1);
    }

    [Test]
    [Category("Unit")]
    public async Task Apply_BookAdded_WhenAuthorNotInBook_ShouldNotIncrement()
    {
        // Arrange
        var authorId = Guid.CreateVersion7();
        var state = CreateState(authorId, 0);

        var @event = new BookAdded(
            Guid.CreateVersion7(),
            "Title",
            "isbn",
            "en",
            [],
            null,
            Guid.CreateVersion7(),
            [Guid.CreateVersion7()], // Different author
            [],
            []);

        // Act
        _projection.Apply(authorId, @event, state);

        // Assert
        _ = await Assert.That(state.BookCount).IsEqualTo(0);
    }

    [Test]
    [Category("Unit")]
    public async Task Apply_BookUpdated_WhenAuthorAdded_ShouldIncrement()
    {
        // Arrange
        var authorId = Guid.CreateVersion7();
        var bookId = Guid.CreateVersion7();
        var state = CreateState(authorId, 5); // Random 5

        // Update event: Author IS in it
        var @event = new BookUpdated(
            bookId,
            "Title",
            "isbn",
            "en",
            [],
            null,
            Guid.CreateVersion7(),
            [authorId], // Added
            [],
            []);

        // Act
        _projection.Apply(authorId, @event, state);

        // Assert
        _ = await Assert.That(state.BookCount).IsEqualTo(6);
    }

    [Test]
    [Category("Unit")]
    public async Task Apply_BookUpdated_WhenAuthorRemoved_ShouldDecrement()
    {
        // Arrange
        var authorId = Guid.CreateVersion7();
        var bookId = Guid.CreateVersion7();
        var state = CreateState(authorId, 5, bookId); // Ensure bookId is in the set

        // Update event: Author is NOT in it
        var @event = new BookUpdated(
            bookId,
            "Title",
            "isbn",
            "en",
            [],
            null,
            Guid.CreateVersion7(),
            [Guid.CreateVersion7()], // Removed
            [],
            []);

        // Act
        _projection.Apply(authorId, @event, state);

        // Assert
        _ = await Assert.That(state.BookCount).IsEqualTo(4);
    }

    [Test]
    [Category("Unit")]
    public async Task Apply_BookSoftDeleted_WhenAuthorInBook_ShouldDecrement()
    {
        // Arrange
        var authorId = Guid.CreateVersion7();
        var bookId = Guid.CreateVersion7();
        var state = CreateState(authorId, 5, bookId); // Ensure bookId is in the set

        var @event = new BookSoftDeleted(bookId, DateTimeOffset.UtcNow);

        // Act
        _projection.Apply(authorId, @event, state);

        // Assert
        _ = await Assert.That(state.BookCount).IsEqualTo(4);
    }

    [Test]
    [Category("Unit")]
    public async Task Apply_BookRestored_WhenAuthorInBook_ShouldIncrement()
    {
        // Arrange
        var authorId = Guid.CreateVersion7();
        var bookId = Guid.CreateVersion7();
        var state = CreateState(authorId, 5); // 5 randoms, doesn't include bookId (assumed deleted)

        var @event = new BookRestored(bookId, DateTimeOffset.UtcNow);

        // Act
        _projection.Apply(authorId, @event, state);

        // Assert
        _ = await Assert.That(state.BookCount).IsEqualTo(6);
    }
}
