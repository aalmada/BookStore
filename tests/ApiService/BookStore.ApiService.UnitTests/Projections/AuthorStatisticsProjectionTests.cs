using BookStore.ApiService.Events;
using BookStore.ApiService.Projections;
using BookStore.Shared.Models;
using Marten;
using NSubstitute;

namespace BookStore.ApiService.UnitTests.Projections;

public class AuthorStatisticsProjectionTests
{
    readonly AuthorStatisticsProjectionBuilder _projection = new();
    readonly IQuerySession _session = Substitute.For<IQuerySession>();

    // Helper to create a projection state
    static AuthorStatistics CreateState(Guid authorId, int count) => new()
    {
        Id = authorId,
        BookCount = count
    };

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
        await _projection.Apply(@event, state, _session);

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
        await _projection.Apply(@event, state, _session);

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
        var state = CreateState(authorId, 5);

        // Previous state of book: Author was NOT in it
        var previousBookState = new BookSearchProjection
        {
            Id = bookId,
            AuthorIds = [Guid.CreateVersion7()]
        };
        _ = _session.LoadAsync<BookSearchProjection>(bookId).Returns(previousBookState);

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
        await _projection.Apply(@event, state, _session);

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
        var state = CreateState(authorId, 5);

        // Previous state of book: Author WAS in it
        var previousBookState = new BookSearchProjection
        {
            Id = bookId,
            AuthorIds = [authorId]
        };
        _ = _session.LoadAsync<BookSearchProjection>(bookId).Returns(previousBookState);

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
        await _projection.Apply(@event, state, _session);

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
        var state = CreateState(authorId, 5);

        var previousBookState = new BookSearchProjection
        {
            Id = bookId,
            AuthorIds = [authorId]
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
    public async Task Apply_BookRestored_WhenAuthorInBook_ShouldIncrement()
    {
        // Arrange
        var authorId = Guid.CreateVersion7();
        var bookId = Guid.CreateVersion7();
        var state = CreateState(authorId, 5);

        var previousBookState = new BookSearchProjection
        {
            Id = bookId,
            AuthorIds = [authorId]
        };
        _ = _session.LoadAsync<BookSearchProjection>(bookId).Returns(previousBookState);

        var @event = new BookRestored(bookId, DateTimeOffset.UtcNow);

        // Act
        await _projection.Apply(@event, state, _session);

        // Assert
        _ = await Assert.That(state.BookCount).IsEqualTo(6);
    }
}
