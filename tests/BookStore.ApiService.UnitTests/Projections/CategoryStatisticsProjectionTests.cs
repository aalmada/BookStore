using BookStore.ApiService.Events;
using BookStore.ApiService.Projections;
using BookStore.Shared.Models;
using Marten;
using NSubstitute;

namespace BookStore.ApiService.UnitTests.Projections;

public class CategoryStatisticsProjectionTests
{
    readonly CategoryStatisticsProjectionBuilder _projection = new();


    static CategoryStatistics CreateState(Guid categoryId, int count, Guid? includeBookId = null)
    {
        var stats = new CategoryStatistics
        {
            Id = categoryId,
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
        _projection.Apply(categoryId, @event, state);

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
        _projection.Apply(categoryId, @event, state);

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
        _projection.Apply(categoryId, @event, state);

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
        var state = CreateState(categoryId, 5, bookId); // Ensure bookId is in the set

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
        _projection.Apply(categoryId, @event, state);

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
        var state = CreateState(categoryId, 5, bookId); // Ensure bookId is in the set

        var @event = new BookSoftDeleted(bookId, DateTimeOffset.UtcNow);

        // Act
        _projection.Apply(categoryId, @event, state);

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


        var @event = new BookRestored(bookId, DateTimeOffset.UtcNow);

        // Act
        _projection.Apply(categoryId, @event, state);

        // Assert
        _ = await Assert.That(state.BookCount).IsEqualTo(6);
    }
}

