using Bogus;
using BookStore.ApiService.Handlers;
using BookStore.ApiService.Messages.Commands;
using BookStore.ApiService.Projections;
using BookStore.Shared.Messages.Events;
using Marten;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace BookStore.ApiService.UnitTests.Handlers;

public class UserCommandHandlerMergeTests
{
    readonly Faker _faker = new();

    [Test]
    [Category("Unit")]
    public async Task MergeAnonymousCart_WithNewProfile_ShouldStartStreamAndAppendMergedItems()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        var bookId = Guid.CreateVersion7();
        var quantity = _faker.Random.Int(1, 4);
        var command = new MergeAnonymousCart(userId, [new CartItemToMerge(bookId, quantity)]);
        var expectedItems = new[] { new AnonymousCartMergedItem(bookId, quantity) };
        var expectedCart = new Dictionary<Guid, int> { [bookId] = quantity };

        var session = Substitute.For<IDocumentSession>();
        _ = session.Events.AggregateStreamAsync<UserProfile>(userId).Returns((UserProfile?)null);

        // Act
        await UserCommandHandler.Handle(command, session, Substitute.For<ILogger>());

        // Assert
        _ = session.Events.Received(1).StartStream<UserProfile>(
            userId,
            Arg.Is<UserProfileCreated>(created => created.UserId == userId));
        _ = session.Events.Received(1).Append(
            userId,
            Arg.Is<AnonymousCartMerged>(@event =>
                EventContainsItems(@event, expectedItems) &&
                ProjectsCart(new UserProfile { Id = userId }, @event, expectedCart)));
    }

    [Test]
    [Category("Unit")]
    public async Task MergeAnonymousCart_WithExistingCartItem_ShouldAccumulateQuantity()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        var bookId = Guid.CreateVersion7();
        var existingQuantity = _faker.Random.Int(1, 4);
        var incomingQuantity = _faker.Random.Int(1, 4);
        var existingProfile = new UserProfile
        {
            Id = userId,
            ShoppingCartItems = new Dictionary<Guid, int> { [bookId] = existingQuantity }
        };
        var command = new MergeAnonymousCart(userId, [new CartItemToMerge(bookId, incomingQuantity)]);
        var expectedItems = new[] { new AnonymousCartMergedItem(bookId, incomingQuantity) };
        var expectedCart = new Dictionary<Guid, int> { [bookId] = existingQuantity + incomingQuantity };

        var session = Substitute.For<IDocumentSession>();
        _ = session.Events.AggregateStreamAsync<UserProfile>(userId).Returns(existingProfile);

        // Act
        await UserCommandHandler.Handle(command, session, Substitute.For<ILogger>());

        // Assert
        _ = session.Events.Received(1).Append(
            userId,
            Arg.Is<AnonymousCartMerged>(@event =>
                EventContainsItems(@event, expectedItems) &&
                ProjectsCart(existingProfile, @event, expectedCart)));
    }

    [Test]
    [Category("Unit")]
    public async Task MergeAnonymousCart_WhenTotalWouldExceedCap_ShouldOnlyMergeRemainingCapacity()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        var bookId = Guid.CreateVersion7();
        var existingProfile = new UserProfile
        {
            Id = userId,
            ShoppingCartItems = new Dictionary<Guid, int> { [bookId] = 7 }
        };
        var command = new MergeAnonymousCart(userId, [new CartItemToMerge(bookId, 6)]);
        var expectedItems = new[] { new AnonymousCartMergedItem(bookId, 3) };
        var expectedCart = new Dictionary<Guid, int> { [bookId] = 10 };

        var session = Substitute.For<IDocumentSession>();
        _ = session.Events.AggregateStreamAsync<UserProfile>(userId).Returns(existingProfile);

        // Act
        await UserCommandHandler.Handle(command, session, Substitute.For<ILogger>());

        // Assert
        _ = session.Events.Received(1).Append(
            userId,
            Arg.Is<AnonymousCartMerged>(@event =>
                EventContainsItems(@event, expectedItems) &&
                ProjectsCart(existingProfile, @event, expectedCart)));
    }

    [Test]
    [Category("Unit")]
    public async Task MergeAnonymousCart_WithEmptyItems_ShouldSkipMerge()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        var existingProfile = new UserProfile { Id = userId };
        var command = new MergeAnonymousCart(userId, []);

        var session = Substitute.For<IDocumentSession>();
        _ = session.Events.AggregateStreamAsync<UserProfile>(userId).Returns(existingProfile);

        // Act
        await UserCommandHandler.Handle(command, session, Substitute.For<ILogger>());

        // Assert
        _ = session.Events.DidNotReceive().StartStream<UserProfile>(Arg.Any<Guid>(), Arg.Any<UserProfileCreated>());
        _ = session.Events.DidNotReceive().Append(userId, Arg.Any<AnonymousCartMerged>());
    }

    [Test]
    [Category("Unit")]
    public async Task MergeAnonymousCart_WithDuplicateIncomingItems_ShouldDeduplicateBeforeAppending()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        var bookId = Guid.CreateVersion7();
        var command = new MergeAnonymousCart(userId,
        [
            new CartItemToMerge(bookId, 2),
            new CartItemToMerge(bookId, 3),
            new CartItemToMerge(bookId, 4)
        ]);
        var expectedItems = new[] { new AnonymousCartMergedItem(bookId, 9) };
        var expectedCart = new Dictionary<Guid, int> { [bookId] = 9 };

        var session = Substitute.For<IDocumentSession>();
        _ = session.Events.AggregateStreamAsync<UserProfile>(userId).Returns(new UserProfile { Id = userId });

        // Act
        await UserCommandHandler.Handle(command, session, Substitute.For<ILogger>());

        // Assert
        _ = session.Events.Received(1).Append(
            userId,
            Arg.Is<AnonymousCartMerged>(@event =>
                @event.Items.Count == 1 &&
                EventContainsItems(@event, expectedItems) &&
                ProjectsCart(new UserProfile { Id = userId }, @event, expectedCart)));
    }

    [Test]
    [Category("Unit")]
    public async Task MergeAnonymousCart_WithMultipleDistinctBooks_ShouldAppendAllBooks()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        var firstBookId = Guid.CreateVersion7();
        var secondBookId = Guid.CreateVersion7();
        var thirdBookId = Guid.CreateVersion7();
        var command = new MergeAnonymousCart(userId,
        [
            new CartItemToMerge(firstBookId, 1),
            new CartItemToMerge(secondBookId, 2),
            new CartItemToMerge(thirdBookId, 3)
        ]);
        var expectedItems = new[]
        {
            new AnonymousCartMergedItem(firstBookId, 1),
            new AnonymousCartMergedItem(secondBookId, 2),
            new AnonymousCartMergedItem(thirdBookId, 3)
        };
        var expectedCart = new Dictionary<Guid, int>
        {
            [firstBookId] = 1,
            [secondBookId] = 2,
            [thirdBookId] = 3
        };

        var session = Substitute.For<IDocumentSession>();
        _ = session.Events.AggregateStreamAsync<UserProfile>(userId).Returns(new UserProfile { Id = userId });

        // Act
        await UserCommandHandler.Handle(command, session, Substitute.For<ILogger>());

        // Assert
        _ = session.Events.Received(1).Append(
            userId,
            Arg.Is<AnonymousCartMerged>(@event =>
                EventContainsItems(@event, expectedItems) &&
                ProjectsCart(new UserProfile { Id = userId }, @event, expectedCart)));
    }

    static bool EventContainsItems(AnonymousCartMerged @event, IReadOnlyCollection<AnonymousCartMergedItem> expectedItems)
        => expectedItems.All(expected => @event.Items.Any(item => item.BookId == expected.BookId && item.Quantity == expected.Quantity))
           && @event.Items.Count == expectedItems.Count;

    static bool ProjectsCart(
        UserProfile existingProfile,
        AnonymousCartMerged @event,
        IReadOnlyDictionary<Guid, int> expectedItems)
    {
        var projectedProfile = new UserProfile
        {
            Id = existingProfile.Id,
            ShoppingCartItems = existingProfile.ShoppingCartItems.ToDictionary(item => item.Key, item => item.Value)
        };

        projectedProfile.Apply(@event);

        return expectedItems.All(expected =>
                   projectedProfile.ShoppingCartItems.TryGetValue(expected.Key, out var quantity) && quantity == expected.Value)
               && projectedProfile.ShoppingCartItems.Count == expectedItems.Count;
    }
}
