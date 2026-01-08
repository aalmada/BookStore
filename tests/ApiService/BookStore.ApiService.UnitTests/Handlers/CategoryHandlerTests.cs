using BookStore.ApiService.Aggregates;
using BookStore.ApiService.Commands;
using BookStore.ApiService.Events;
using BookStore.ApiService.Handlers.Categories;
using BookStore.ApiService.Infrastructure;
using BookStore.Shared.Models;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace BookStore.ApiService.UnitTests.Handlers;

public class CategoryHandlerTests
{
    [Test]
    [Category("Unit")]
    public async Task CreateCategoryHandler_ShouldStartStreamWithCategoryAddedEvent()
    {
        // Arrange
        var command = new CreateCategory(
            new Dictionary<string, CategoryTranslationDto>
            {
                ["en"] = new CategoryTranslationDto("Technology", "Books about tech")
            }
        );

        var session = Substitute.For<IDocumentSession>();
        _ = session.CorrelationId.Returns("test-correlation-id");

        // Act
        var result = CategoryHandlers.Handle(command, session, Substitute.For<ILogger<CreateCategory>>());

        // Assert
        _ = await Assert.That(result).IsNotNull();
        // Notification verification via listener not applicable here as unit test invokes handler directly?
        // Wait, handler appends event. We check session.Events.StartStream. That is enough.
        _ = session.Events.Received(1).StartStream<CategoryAggregate>(
            command.Id,
            Arg.Is<CategoryAdded>(e =>
                e.Translations["en"].Name == "Technology"));
    }

    [Test]
    [Category("Unit")]
    [Arguments("invalid-culture", 10, 10)]
    [Arguments("en", 101, 10)]
    [Arguments("en", 10, 501)]
    public async Task CreateCategoryHandler_WithInvalidData_ShouldReturnBadRequest(string culture, int nameLength, int descLength)
    {
        // Arrange
        var name = new string('a', nameLength);
        var description = new string('a', descLength);

        var command = new CreateCategory(
            new Dictionary<string, CategoryTranslationDto>
            {
                [culture] = new CategoryTranslationDto(name, description)
            }
        );

        var session = Substitute.For<IDocumentSession>();

        // Act
        // Act
        var result = CategoryHandlers.Handle(command, session, Substitute.For<ILogger<CreateCategory>>());

        // Assert
        _ = await Assert.That(result).IsAssignableTo<Microsoft.AspNetCore.Http.IStatusCodeHttpResult>();
        var badRequestResult = (Microsoft.AspNetCore.Http.IStatusCodeHttpResult)result;
        _ = await Assert.That(badRequestResult.StatusCode).IsEqualTo(400);
    }

    [Test]
    [Category("Unit")]
    public async Task UpdateCategoryHandler_ShouldAppendCategoryUpdatedEvent()
    {
        // Arrange
        var command = new UpdateCategory(
            Guid.CreateVersion7(),
            new Dictionary<string, CategoryTranslationDto>
            {
                ["en"] = new CategoryTranslationDto("Technology Updated", "Updated desc")
            }
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
        var existingAggregate = CreateCategoryAggregate(command.Id, "Old Tech", false);
        _ = session.Events.AggregateStreamAsync<CategoryAggregate>(command.Id).Returns(existingAggregate);

        // Act
        // Act
        var result = await CategoryHandlers.Handle(command, session, httpContextAccessor, Substitute.For<ILogger<UpdateCategory>>());

        // Assert
        _ = await Assert.That(result).IsTypeOf<Microsoft.AspNetCore.Http.HttpResults.NoContent>();
        _ = session.Events.Received(1).Append(
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

        var session = Substitute.For<IDocumentSession>();
        var httpContext = new DefaultHttpContext();
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        _ = httpContextAccessor.HttpContext.Returns(httpContext);

        // Mock Stream State
        _ = session.Events.FetchStreamStateAsync(id).Returns(new Marten.Events.StreamState { Version = 1 });

        // Mock Aggregate Load
        var existingAggregate = CreateCategoryAggregate(id, "Tech", false);
        _ = session.Events.AggregateStreamAsync<CategoryAggregate>(id).Returns(existingAggregate);

        // Act
        var result = await CategoryHandlers.Handle(command, session, httpContextAccessor, Substitute.For<ILogger<SoftDeleteCategory>>());

        // Assert
        _ = await Assert.That(result).IsTypeOf<Microsoft.AspNetCore.Http.HttpResults.NoContent>();
        _ = session.Events.Received(1).Append(
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

        var session = Substitute.For<IDocumentSession>();
        var httpContext = new DefaultHttpContext();
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        _ = httpContextAccessor.HttpContext.Returns(httpContext);

        // Mock Stream State
        _ = session.Events.FetchStreamStateAsync(id).Returns(new Marten.Events.StreamState { Version = 1 });

        // Mock Aggregate Load - Create DELETED aggregate
        var existingAggregate = CreateCategoryAggregate(id, "Tech", true);
        _ = session.Events.AggregateStreamAsync<CategoryAggregate>(id).Returns(existingAggregate);

        // Act
        var result = await CategoryHandlers.Handle(command, session, httpContextAccessor, Substitute.For<ILogger<RestoreCategory>>());

        // Assert
        _ = await Assert.That(result).IsTypeOf<Microsoft.AspNetCore.Http.HttpResults.NoContent>();
        _ = session.Events.Received(1).Append(
            id,
            Arg.Is<CategoryRestored>(e => e.Id == id));
    }

    static CategoryAggregate CreateCategoryAggregate(Guid id, string defaultName, bool isDeleted)
    {
        var aggregate = (CategoryAggregate)Activator.CreateInstance(typeof(CategoryAggregate), true)!;

        typeof(CategoryAggregate).GetProperty(nameof(CategoryAggregate.Id))!.SetValue(aggregate, id);
        typeof(CategoryAggregate).GetProperty(nameof(CategoryAggregate.IsDeleted))!.SetValue(aggregate, isDeleted);
        typeof(CategoryAggregate).GetProperty(nameof(CategoryAggregate.Translations))!.SetValue(aggregate, new Dictionary<string, CategoryTranslation>
        {
            ["en"] = new(defaultName, "Description")
        });

        return aggregate;
    }
}
