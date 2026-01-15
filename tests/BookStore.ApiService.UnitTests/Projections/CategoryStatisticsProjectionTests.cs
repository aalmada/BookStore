using BookStore.ApiService.Events;
using BookStore.ApiService.Projections;
using BookStore.Shared.Models;
using Marten;
using NSubstitute;

namespace BookStore.ApiService.UnitTests.Projections;

public class CategoryStatisticsProjectionTests
{
    readonly CategoryStatisticsProjectionBuilder _projection = new();
    readonly IQuerySession _session = Substitute.For<IQuerySession>();

    static CategoryStatistics CreateState(Guid categoryId, int count) => new()
    {
        Id = categoryId,
        BookCount = count
    };

    [Test]
    [Category("Unit")]
    public async Task Create_ShouldInitializeCountToZero()
    {
        // Arrange
        var categoryId = Guid.CreateVersion7();
        var @event = new CategoryAdded(
            categoryId,
            [],
            DateTimeOffset.UtcNow);

        // Act
        var result = _projection.Create(@event);

        // Assert
        _ = await Assert.That(result.Id).IsEqualTo(categoryId);
        _ = await Assert.That(result.BookCount).IsEqualTo(0);
    }

    [Test]
    [Category("Unit")]
    public async Task Apply_BookAdded_WhenCategoryInBook_ShouldIncrement()
    {
        // Arrange
        var categoryId = Guid.CreateVersion7();
        var state = CreateState(categoryId, 0);

        var @event = new BookAdded(
            Guid.CreateVersion7(),
            "Title",
            "isbn",
            "en",
            [],
            null,
            Guid.CreateVersion7(),
            [],
            [categoryId], // Category is included
            []);

        // Act
        await _projection.Apply(@event, state, _session);

        // Assert
        _ = await Assert.That(state.BookCount).IsEqualTo(1);
    }

    [Test]
    [Category("Unit")]
    public async Task Apply_BookAdded_WhenCategoryNotInBook_ShouldNotIncrement()
    {
        // Arrange
        var categoryId = Guid.CreateVersion7();
        var state = CreateState(categoryId, 0);

        var @event = new BookAdded(
            Guid.CreateVersion7(),
            "Title",
            "isbn",
            "en",
            [],
            null,
            Guid.CreateVersion7(),
            [],
            [Guid.CreateVersion7()], // Different category
            []);

        // Act
        await _projection.Apply(@event, state, _session);

        // Assert
        _ = await Assert.That(state.BookCount).IsEqualTo(0);
    }

    [Test]
    [Category("Unit")]
    public async Task Apply_BookUpdated_WhenCategoryAdded_ShouldIncrement()
    {
        // Arrange
        var categoryId = Guid.CreateVersion7();
        var bookId = Guid.CreateVersion7();
        var state = CreateState(categoryId, 5);

        // Previous state of book: Category was NOT in it
        var previousBookState = new BookSearchProjection
        {
            Id = bookId,
            CategoryIds = [Guid.CreateVersion7()]
        };
        _ = _session.LoadAsync<BookSearchProjection>(bookId).Returns(previousBookState);

        // Update event: Category IS in it
        var @event = new BookUpdated(
            bookId,
            "Title",
            "isbn",
            "en",
            [],
            null,
            Guid.CreateVersion7(),
            [],
            [categoryId], // Added
            []);

        // Act
        await _projection.Apply(@event, state, _session);

        // Assert
        _ = await Assert.That(state.BookCount).IsEqualTo(6);
    }

    [Test]
    [Category("Unit")]
    public async Task Apply_BookUpdated_WhenCategoryRemoved_ShouldDecrement()
    {
        // Arrange
        var categoryId = Guid.CreateVersion7();
        var bookId = Guid.CreateVersion7();
        var state = CreateState(categoryId, 5);

        // Previous state of book: Category WAS in it
        var previousBookState = new BookSearchProjection
        {
            Id = bookId,
            CategoryIds = [categoryId]
        };
        _ = _session.LoadAsync<BookSearchProjection>(bookId).Returns(previousBookState);

        // Update event: Category is NOT in it
        var @event = new BookUpdated(
            bookId,
            "Title",
            "isbn",
            "en",
            [],
            null,
            Guid.CreateVersion7(),
            [],
            [Guid.CreateVersion7()], // Removed
            []);

        // Act
        await _projection.Apply(@event, state, _session);

        // Assert
        _ = await Assert.That(state.BookCount).IsEqualTo(4);
    }

    [Test]
    [Category("Unit")]
    public async Task Apply_BookSoftDeleted_WhenCategoryInBook_ShouldDecrement()
    {
        // Arrange
        var categoryId = Guid.CreateVersion7();
        var bookId = Guid.CreateVersion7();
        var state = CreateState(categoryId, 5);

        var previousBookState = new BookSearchProjection
        {
            Id = bookId,
            CategoryIds = [categoryId]
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
    public async Task Apply_BookRestored_WhenCategoryInBook_ShouldIncrement()
    {
        // Arrange
        var categoryId = Guid.CreateVersion7();
        var bookId = Guid.CreateVersion7();
        var state = CreateState(categoryId, 5);

        var previousBookState = new BookSearchProjection
        {
            Id = bookId,
            CategoryIds = [categoryId]
        };
        _ = _session.LoadAsync<BookSearchProjection>(bookId).Returns(previousBookState);

        var @event = new BookRestored(bookId, DateTimeOffset.UtcNow);

        // Act
        await _projection.Apply(@event, state, _session);

        // Assert
        _ = await Assert.That(state.BookCount).IsEqualTo(6);
    }
}
