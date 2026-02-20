using BookStore.ApiService.Aggregates;
using BookStore.ApiService.Commands;
using BookStore.ApiService.Events;
using BookStore.ApiService.Handlers.Categories;
using BookStore.ApiService.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace BookStore.ApiService.UnitTests.Handlers;

public class CategoryHandlerTests : HandlerTestBase
{
    [Test]
    [Category("Unit")]
    public async Task CreateCategoryHandler_ShouldStartStreamWithCategoryAddedEvent()
    {
        // Arrange
        var command = new CreateCategory(
            Guid.CreateVersion7(),
            new Dictionary<string, CategoryTranslationDto> { ["en"] = new CategoryTranslationDto("Technology") }
        );

        // Act
        var result = await CategoryHandlers.Handle(command, Session, Cache, GetLogger<CreateCategory>());

        // Assert
        _ = await Assert.That(result).IsNotNull();
        _ = Session.Events.Received(1).StartStream<CategoryAggregate>(
            command.Id,
            Arg.Is<CategoryAdded>(e =>
                e.Translations["en"].Name == "Technology"));
    }

    [Test]
    [Category("Unit")]
    [Arguments("invalid-culture", 10)]
    [Arguments("en", 101)]
    public async Task CreateCategoryHandler_WithInvalidData_ShouldReturnBadRequest(string culture, int nameLength)
    {
        // Arrange
        var name = new string('a', nameLength);

        var command = new CreateCategory(
            Guid.CreateVersion7(),
            new Dictionary<string, CategoryTranslationDto> { [culture] = new CategoryTranslationDto(name) }
        );

        // Act
        var result = await CategoryHandlers.Handle(command, Session, Cache, GetLogger<CreateCategory>());

        // Assert
        _ = await Assert.That(result).IsAssignableTo<IStatusCodeHttpResult>();
        var badRequestResult = (IStatusCodeHttpResult)result;
        _ = await Assert.That(badRequestResult.StatusCode).IsEqualTo(400);
    }

    [Test]
    [Category("Unit")]
    public async Task UpdateCategoryHandler_ShouldAppendCategoryUpdatedEvent()
    {
        // Arrange
        var command = new UpdateCategory(
            Guid.CreateVersion7(),
            new Dictionary<string, CategoryTranslationDto> { ["en"] = new CategoryTranslationDto("Technology Updated") }
        )
        { ETag = "\"1\"" };

        // Mock Stream State
        _ = Session.Events.FetchStreamStateAsync(command.Id).Returns(new Marten.Events.StreamState { Version = 1 });

        // Mock Aggregate Load
        var existingAggregate = AggregateFactory.Hydrate<CategoryAggregate>(
            new CategoryAdded(command.Id, new Dictionary<string, CategoryTranslation> { ["en"] = new("Old Tech") },
                DateTimeOffset.UtcNow));
        _ = Session.Events.AggregateStreamAsync<CategoryAggregate>(command.Id).Returns(existingAggregate);

        // Act
        var result =
            await CategoryHandlers.Handle(command, Session, Cache, GetLogger<UpdateCategory>());

        // Assert
        _ = await Assert.That(result).IsTypeOf<NoContent>();
        _ = Session.Events.Received(1).Append(
            command.Id,
            Arg.Is<CategoryUpdated>(e =>
                e.Translations["en"].Name == "Technology Updated"));
    }

    [Test]
    [Category("Unit")]
    public async Task SoftDeleteCategoryHandler_ShouldAppendCategorySoftDeletedEvent()
    {
        // Arrange
        var id = Guid.CreateVersion7();
        var command = new SoftDeleteCategory(id);

        // Mock Stream State
        _ = Session.Events.FetchStreamStateAsync(id).Returns(new Marten.Events.StreamState { Version = 1 });

        // Mock Aggregate Load
        var existingAggregate = AggregateFactory.Hydrate<CategoryAggregate>(
            new CategoryAdded(id, new Dictionary<string, CategoryTranslation> { ["en"] = new("Tech") },
                DateTimeOffset.UtcNow));
        _ = Session.Events.AggregateStreamAsync<CategoryAggregate>(id).Returns(existingAggregate);

        // Act
        var result = await CategoryHandlers.Handle(command, Session, Cache,
            GetLogger<SoftDeleteCategory>());

        // Assert
        _ = await Assert.That(result).IsTypeOf<NoContent>();
        _ = Session.Events.Received(1).Append(
            id,
            Arg.Is<CategorySoftDeleted>(e => e.Id == id));
    }

    [Test]
    [Category("Unit")]
    public async Task RestoreCategoryHandler_ShouldAppendCategoryRestoredEvent()
    {
        // Arrange
        var id = Guid.CreateVersion7();
        var command = new RestoreCategory(id);

        // Mock Stream State
        _ = Session.Events.FetchStreamStateAsync(id).Returns(new Marten.Events.StreamState { Version = 1 });

        // Mock Aggregate Load - Create DELETED aggregate
        var existingAggregate = AggregateFactory.Hydrate<CategoryAggregate>(
            new CategoryAdded(id, new Dictionary<string, CategoryTranslation> { ["en"] = new("Tech") },
                DateTimeOffset.UtcNow),
            new CategorySoftDeleted(id, DateTimeOffset.UtcNow));
        _ = Session.Events.AggregateStreamAsync<CategoryAggregate>(id).Returns(existingAggregate);

        // Act
        var result =
            await CategoryHandlers.Handle(command, Session, Cache, GetLogger<RestoreCategory>());

        // Assert
        _ = await Assert.That(result).IsTypeOf<NoContent>();
        _ = Session.Events.Received(1).Append(
            id,
            Arg.Is<CategoryRestored>(e => e.Id == id));
    }
}
