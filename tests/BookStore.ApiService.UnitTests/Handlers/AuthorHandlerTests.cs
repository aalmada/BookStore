using BookStore.ApiService.Aggregates;
using BookStore.ApiService.Commands;
using BookStore.ApiService.Events;
using BookStore.ApiService.Handlers.Authors;
using BookStore.ApiService.Infrastructure;
using BookStore.Shared.Models;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace BookStore.ApiService.UnitTests.Handlers;

public class AuthorHandlerTests
{
    [Test]
    [Category("Unit")]
    public async Task CreateAuthorHandler_ShouldStartStreamWithAuthorAddedEvent()
    {
        // Arrange
        var command = new CreateAuthor(
            "Robert C. Martin",
            new Dictionary<string, AuthorTranslationDto> { ["en"] = new AuthorTranslationDto("Uncle Bob") }
        );

        var session = Substitute.For<IDocumentSession>();
        _ = session.CorrelationId.Returns("test-correlation-id");

        var localizationOptions = Options.Create(new LocalizationOptions
        {
            DefaultCulture = "en",
            SupportedCultures = ["en"]
        });

        // Act
        var result = await AuthorHandlers.Handle(command, session, localizationOptions, Substitute.For<HybridCache>(),
            Substitute.For<ILogger>());

        // Assert
        _ = await Assert.That(result).IsNotNull();
        _ = session.Events.Received(1).StartStream<AuthorAggregate>(
            command.Id,
            Arg.Is<AuthorAdded>(e =>
                e.Name == "Robert C. Martin"));
    }

    [Test]
    [Category("Unit")]
    [Arguments("invalid-culture")]
    [Arguments("xx-XX")]
    [Arguments("123")]
    public async Task CreateAuthorHandler_WithInvalidCulture_ShouldReturnBadRequest(string invalidCulture)
    {
        // Arrange
        var command = new CreateAuthor(
            "Robert C. Martin",
            new Dictionary<string, AuthorTranslationDto> { [invalidCulture] = new AuthorTranslationDto("Uncle Bob") }
        );

        var session = Substitute.For<IDocumentSession>();
        var localizationOptions = Options.Create(new LocalizationOptions
        {
            DefaultCulture = "en",
            SupportedCultures = ["en"]
        });

        // Act
        var result = await AuthorHandlers.Handle(command, session, localizationOptions, Substitute.For<HybridCache>(),
            Substitute.For<ILogger>());

        // Assert
        // Assert
        _ = await Assert.That(result).IsAssignableTo<Microsoft.AspNetCore.Http.IStatusCodeHttpResult>();
        var badRequestResult = (Microsoft.AspNetCore.Http.IStatusCodeHttpResult)result;
        _ = await Assert.That(badRequestResult.StatusCode).IsEqualTo(400);
    }

    [Test]
    [Category("Unit")]
    public async Task UpdateAuthorHandler_ShouldAppendAuthorUpdatedEvent()
    {
        // Arrange
        var command = new UpdateAuthor(
            Guid.CreateVersion7(),
            "Robert C. Martin Updated",
            new Dictionary<string, AuthorTranslationDto> { ["en"] = new AuthorTranslationDto("Uncle Bob Updated") }
        )
        { ETag = "test-etag" };

        var session = Substitute.For<IDocumentSession>();
        var httpContext = new DefaultHttpContext();
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        _ = httpContextAccessor.HttpContext.Returns(httpContext);

        var localizationOptions = Options.Create(new LocalizationOptions
        {
            DefaultCulture = "en",
            SupportedCultures = ["en"]
        });

        // Mock Stream State
        _ = session.Events.FetchStreamStateAsync(command.Id).Returns(new Marten.Events.StreamState { Version = 1 });

        // Mock Aggregate Load
        var existingAggregate = CreateAuthorAggregate(command.Id, "Old Name", false);
        _ = session.Events.AggregateStreamAsync<AuthorAggregate>(command.Id).Returns(existingAggregate);

        // Act
        var result = await AuthorHandlers.Handle(command, session, httpContextAccessor, localizationOptions,
            Substitute.For<HybridCache>(), Substitute.For<ILogger>());

        // Assert
        _ = await Assert.That(result).IsTypeOf<Microsoft.AspNetCore.Http.HttpResults.NoContent>();
        _ = session.Events.Received(1).Append(
            command.Id,
            Arg.Is<AuthorUpdated>(e =>
                e.Name == "Robert C. Martin Updated"));
    }

    [Test]
    [Category("Unit")]
    public async Task SoftDeleteAuthorHandler_ShouldAppendAuthorSoftDeletedEvent()
    {
        // Arrange
        var id = Guid.CreateVersion7();
        var command = new SoftDeleteAuthor(id);

        var session = Substitute.For<IDocumentSession>();
        var httpContext = new DefaultHttpContext();
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        _ = httpContextAccessor.HttpContext.Returns(httpContext);

        // Mock Stream State
        _ = session.Events.FetchStreamStateAsync(id).Returns(new Marten.Events.StreamState { Version = 1 });

        // Mock Aggregate Load
        var existingAggregate = CreateAuthorAggregate(id, "Author", false);
        _ = session.Events.AggregateStreamAsync<AuthorAggregate>(id).Returns(existingAggregate);

        // Act
        var result = await AuthorHandlers.Handle(command, session, httpContextAccessor, Substitute.For<HybridCache>(),
            Substitute.For<ILogger>());

        // Assert
        _ = await Assert.That(result).IsTypeOf<Microsoft.AspNetCore.Http.HttpResults.NoContent>();
        _ = session.Events.Received(1).Append(
            id,
            Arg.Is<AuthorSoftDeleted>(e => e.Id == id));
    }

    [Test]
    [Category("Unit")]
    public async Task RestoreAuthorHandler_ShouldAppendAuthorRestoredEvent()
    {
        // Arrange
        var id = Guid.CreateVersion7();
        var command = new RestoreAuthor(id);

        var session = Substitute.For<IDocumentSession>();
        var httpContext = new DefaultHttpContext();
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        _ = httpContextAccessor.HttpContext.Returns(httpContext);

        // Mock Stream State
        _ = session.Events.FetchStreamStateAsync(id).Returns(new Marten.Events.StreamState { Version = 1 });

        // Mock Aggregate Load - Create DELETED aggregate
        var existingAggregate = CreateAuthorAggregate(id, "Author", true);
        _ = session.Events.AggregateStreamAsync<AuthorAggregate>(id).Returns(existingAggregate);

        // Act
        var result = await AuthorHandlers.Handle(command, session, httpContextAccessor, Substitute.For<HybridCache>(),
            Substitute.For<ILogger>());

        // Assert
        _ = await Assert.That(result).IsTypeOf<Microsoft.AspNetCore.Http.HttpResults.NoContent>();
        _ = session.Events.Received(1).Append(
            id,
            Arg.Is<AuthorRestored>(e => e.Id == id));
    }

    static AuthorAggregate CreateAuthorAggregate(Guid id, string name, bool isDeleted)
    {
        // Create instance (AuthorAggregate has parameterless constructor but it might be private/internal?)
        // It's public class, but properties are private set.
        // Assuming default constructor is accessible or we use Activator.
        var aggregate = (AuthorAggregate)Activator.CreateInstance(typeof(AuthorAggregate), true)!;

        // Set properties via reflection
        typeof(AuthorAggregate).GetProperty(nameof(AuthorAggregate.Id))!.SetValue(aggregate, id);
        typeof(AuthorAggregate).GetProperty(nameof(AuthorAggregate.Name))!.SetValue(aggregate, name);
        typeof(AuthorAggregate).GetProperty(nameof(AuthorAggregate.Deleted))!.SetValue(aggregate, isDeleted);
        typeof(AuthorAggregate).GetProperty(nameof(AuthorAggregate.Translations))!.SetValue(aggregate,
            new Dictionary<string, AuthorTranslation> { ["en"] = new("Bio") });

        return aggregate;
    }
}
