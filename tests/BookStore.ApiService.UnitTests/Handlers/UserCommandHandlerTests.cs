using BookStore.ApiService.Handlers;
using BookStore.ApiService.Messages.Commands;
using BookStore.ApiService.Projections;
using BookStore.Shared.Messages.Events;
using Marten;
using NSubstitute;
using System.Threading;

#pragma warning disable CS8602 // Dereference of a possibly null reference - Exception.Message is never null for our test scenarios

namespace BookStore.ApiService.UnitTests.Handlers;

public class UserCommandHandlerTests
{
    #region AddBookToFavorites Tests

    [Test]
    [Category("Unit")]
    public async Task AddBookToFavorites_WithNullProfile_ShouldAppendEvent()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        var bookId = Guid.CreateVersion7();
        var command = new AddBookToFavorites(userId, bookId);

        var session = Substitute.For<IDocumentSession>();
        _ = session.LoadAsync<UserProfile>(userId, Arg.Any<CancellationToken>()).Returns((UserProfile?)null);

        // Act
        await UserCommandHandler.Handle(command, session);

        // Assert
        _ = session.Events.Received(1).Append(
            userId,
            Arg.Is<BookAddedToFavorites>(e => e.BookId == bookId));
    }

    [Test]
    [Category("Unit")]
    public async Task AddBookToFavorites_WithExistingProfile_ShouldAppendEvent()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        var bookId = Guid.CreateVersion7();
        var command = new AddBookToFavorites(userId, bookId);

        var session = Substitute.For<IDocumentSession>();
        var existingProfile = new UserProfile
        {
            Id = userId,
            FavoriteBookIds = [Guid.CreateVersion7()] // Different book
        };
        _ = session.LoadAsync<UserProfile>(userId, Arg.Any<CancellationToken>()).Returns(existingProfile);

        // Act
        await UserCommandHandler.Handle(command, session);

        // Assert
        _ = session.Events.Received(1).Append(
            userId,
            Arg.Is<BookAddedToFavorites>(e => e.BookId == bookId));
    }

    [Test]
    [Category("Unit")]
    public async Task AddBookToFavorites_WhenAlreadyFavorite_ShouldNotAppendEvent()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        var bookId = Guid.CreateVersion7();
        var command = new AddBookToFavorites(userId, bookId);

        var session = Substitute.For<IDocumentSession>();
        var existingProfile = new UserProfile
        {
            Id = userId,
            FavoriteBookIds = [bookId] // Already in favorites
        };
        _ = session.LoadAsync<UserProfile>(userId, Arg.Any<CancellationToken>()).Returns(existingProfile);

        // Act
        await UserCommandHandler.Handle(command, session);

        // Assert
        _ = session.Events.DidNotReceive().Append(
            userId,
            Arg.Any<BookAddedToFavorites>());
    }

    #endregion

    #region RemoveBookFromFavorites Tests

    [Test]
    [Category("Unit")]
    public async Task RemoveBookFromFavorites_WithExistingFavorite_ShouldAppendEvent()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        var bookId = Guid.CreateVersion7();
        var command = new RemoveBookFromFavorites(userId, bookId);

        var session = Substitute.For<IDocumentSession>();
        var existingProfile = new UserProfile
        {
            Id = userId,
            FavoriteBookIds = [bookId]
        };
        _ = session.LoadAsync<UserProfile>(userId, Arg.Any<CancellationToken>()).Returns(existingProfile);

        // Act
        await UserCommandHandler.Handle(command, session);

        // Assert
        _ = session.Events.Received(1).Append(
            userId,
            Arg.Is<BookRemovedFromFavorites>(e => e.BookId == bookId));
    }

    [Test]
    [Category("Unit")]
    public async Task RemoveBookFromFavorites_WhenNotFavorite_ShouldNotAppendEvent()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        var bookId = Guid.CreateVersion7();
        var command = new RemoveBookFromFavorites(userId, bookId);

        var session = Substitute.For<IDocumentSession>();
        var existingProfile = new UserProfile
        {
            Id = userId,
            FavoriteBookIds = [Guid.CreateVersion7()] // Different book
        };
        _ = session.LoadAsync<UserProfile>(userId, Arg.Any<CancellationToken>()).Returns(existingProfile);

        // Act
        await UserCommandHandler.Handle(command, session);

        // Assert
        _ = session.Events.DidNotReceive().Append(
            userId,
            Arg.Any<BookRemovedFromFavorites>());
    }

    [Test]
    [Category("Unit")]
    public async Task RemoveBookFromFavorites_WithNullProfile_ShouldNotAppendEvent()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        var bookId = Guid.CreateVersion7();
        var command = new RemoveBookFromFavorites(userId, bookId);

        var session = Substitute.For<IDocumentSession>();
        _ = session.LoadAsync<UserProfile>(userId, Arg.Any<CancellationToken>()).Returns((UserProfile?)null);

        // Act
        await UserCommandHandler.Handle(command, session);

        // Assert
        _ = session.Events.DidNotReceive().Append(
            userId,
            Arg.Any<BookRemovedFromFavorites>());
    }

    #endregion

    #region RateBook Tests

    [Test]
    [Category("Unit")]
    [Arguments(1)]
    [Arguments(3)]
    [Arguments(5)]
    public async Task RateBook_WithValidRating_ShouldAppendEvent(int rating)
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        var bookId = Guid.CreateVersion7();
        var command = new RateBook(userId, bookId, rating);

        var session = Substitute.For<IDocumentSession>();
        var existingProfile = new UserProfile { Id = userId };
        _ = session.LoadAsync<UserProfile>(userId, Arg.Any<CancellationToken>()).Returns(existingProfile);

        // Act
        await UserCommandHandler.Handle(command, session);

        // Assert
        _ = session.Events.Received(1).Append(
            userId,
            Arg.Is<BookRated>(e => e.BookId == bookId && e.Rating == rating));
    }

    [Test]
    [Category("Unit")]
    public async Task RateBook_WithZeroRating_ShouldThrowArgumentException()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        var bookId = Guid.CreateVersion7();
        var command = new RateBook(userId, bookId, 0);

        var session = Substitute.For<IDocumentSession>();

        // Act & Assert
        var exception = await Assert.That(async () => await UserCommandHandler.Handle(command, session))
            .Throws<ArgumentException>();
        _ = await Assert.That(exception.Message!).Contains("Rating must be between 1 and 5");
    }

    [Test]
    [Category("Unit")]
    public async Task RateBook_WithNegativeRating_ShouldThrowArgumentException()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        var bookId = Guid.CreateVersion7();
        var command = new RateBook(userId, bookId, -1);

        var session = Substitute.For<IDocumentSession>();

        // Act & Assert
        var exception = await Assert.That(async () => await UserCommandHandler.Handle(command, session))
            .Throws<ArgumentException>();
        _ = await Assert.That(exception.Message!).Contains("Rating must be between 1 and 5");
    }

    [Test]
    [Category("Unit")]
    public async Task RateBook_WithRatingAboveFive_ShouldThrowArgumentException()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        var bookId = Guid.CreateVersion7();
        var command = new RateBook(userId, bookId, 6);

        var session = Substitute.For<IDocumentSession>();

        // Act & Assert
        var exception = await Assert.That(async () => await UserCommandHandler.Handle(command, session))
            .Throws<ArgumentException>();
        _ = await Assert.That(exception.Message!).Contains("Rating must be between 1 and 5");
    }

    [Test]
    [Category("Unit")]
    public async Task RateBook_UpdatingExistingRating_ShouldAppendEvent()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        var bookId = Guid.CreateVersion7();
        var command = new RateBook(userId, bookId, 4);

        var session = Substitute.For<IDocumentSession>();
        var existingProfile = new UserProfile
        {
            Id = userId,
            BookRatings = new Dictionary<Guid, int> { [bookId] = 2 } // Previous rating
        };
        _ = session.LoadAsync<UserProfile>(userId, Arg.Any<CancellationToken>()).Returns(existingProfile);

        // Act
        await UserCommandHandler.Handle(command, session);

        // Assert - Event should still be appended for update
        _ = session.Events.Received(1).Append(
            userId,
            Arg.Is<BookRated>(e => e.BookId == bookId && e.Rating == 4));
    }

    [Test]
    [Category("Unit")]
    public async Task RateBook_WithNullProfile_ShouldAppendEvent()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        var bookId = Guid.CreateVersion7();
        var command = new RateBook(userId, bookId, 5);

        var session = Substitute.For<IDocumentSession>();
        _ = session.LoadAsync<UserProfile>(userId, Arg.Any<CancellationToken>()).Returns((UserProfile?)null);

        // Act
        await UserCommandHandler.Handle(command, session);

        // Assert
        _ = session.Events.Received(1).Append(
            userId,
            Arg.Is<BookRated>(e => e.BookId == bookId && e.Rating == 5));
    }

    #endregion

    #region RemoveBookRating Tests

    [Test]
    [Category("Unit")]
    public async Task RemoveBookRating_WithExistingRating_ShouldAppendEvent()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        var bookId = Guid.CreateVersion7();
        var command = new RemoveBookRating(userId, bookId);

        var session = Substitute.For<IDocumentSession>();
        var existingProfile = new UserProfile
        {
            Id = userId,
            BookRatings = new Dictionary<Guid, int> { [bookId] = 4 }
        };
        _ = session.LoadAsync<UserProfile>(userId, Arg.Any<CancellationToken>()).Returns(existingProfile);

        // Act
        await UserCommandHandler.Handle(command, session);

        // Assert
        _ = session.Events.Received(1).Append(
            userId,
            Arg.Is<BookRatingRemoved>(e => e.BookId == bookId));
    }

    [Test]
    [Category("Unit")]
    public async Task RemoveBookRating_WithoutExistingRating_ShouldNotAppendEvent()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        var bookId = Guid.CreateVersion7();
        var command = new RemoveBookRating(userId, bookId);

        var session = Substitute.For<IDocumentSession>();
        var existingProfile = new UserProfile
        {
            Id = userId,
            BookRatings = [] // No ratings
        };
        _ = session.LoadAsync<UserProfile>(userId, Arg.Any<CancellationToken>()).Returns(existingProfile);

        // Act
        await UserCommandHandler.Handle(command, session);

        // Assert
        _ = session.Events.DidNotReceive().Append(
            userId,
            Arg.Any<BookRatingRemoved>());
    }

    [Test]
    [Category("Unit")]
    public async Task RemoveBookRating_WithNullProfile_ShouldNotAppendEvent()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        var bookId = Guid.CreateVersion7();
        var command = new RemoveBookRating(userId, bookId);

        var session = Substitute.For<IDocumentSession>();
        _ = session.LoadAsync<UserProfile>(userId, Arg.Any<CancellationToken>()).Returns((UserProfile?)null);

        // Act
        await UserCommandHandler.Handle(command, session);

        // Assert
        _ = session.Events.DidNotReceive().Append(
            userId,
            Arg.Any<BookRatingRemoved>());
    }

    #endregion

    #region AddBookToCart Tests

    [Test]
    [Category("Unit")]
    public async Task AddBookToCart_WithValidQuantity_ShouldAppendEvent()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        var bookId = Guid.CreateVersion7();
        var command = new AddBookToCart(userId, bookId, 3);

        var session = Substitute.For<IDocumentSession>();
        var existingProfile = new UserProfile { Id = userId };
        _ = session.LoadAsync<UserProfile>(userId, Arg.Any<CancellationToken>()).Returns(existingProfile);

        // Act
        await UserCommandHandler.Handle(command, session);

        // Assert
        _ = session.Events.Received(1).Append(
            userId,
            Arg.Is<BookAddedToCart>(e => e.BookId == bookId && e.Quantity == 3));
    }

    [Test]
    [Category("Unit")]
    public async Task AddBookToCart_WithZeroQuantity_ShouldThrowArgumentException()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        var bookId = Guid.CreateVersion7();
        var command = new AddBookToCart(userId, bookId, 0);

        var session = Substitute.For<IDocumentSession>();

        // Act & Assert
        var exception = await Assert.That(async () => await UserCommandHandler.Handle(command, session))
            .Throws<ArgumentException>();
        _ = await Assert.That(exception.Message!).Contains("Quantity must be greater than 0");
    }

    [Test]
    [Category("Unit")]
    public async Task AddBookToCart_WithNegativeQuantity_ShouldThrowArgumentException()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        var bookId = Guid.CreateVersion7();
        var command = new AddBookToCart(userId, bookId, -5);

        var session = Substitute.For<IDocumentSession>();

        // Act & Assert
        var exception = await Assert.That(async () => await UserCommandHandler.Handle(command, session))
            .Throws<ArgumentException>();
        _ = await Assert.That(exception.Message!).Contains("Quantity must be greater than 0");
    }

    [Test]
    [Category("Unit")]
    public async Task AddBookToCart_WithExistingCartItem_ShouldAppendEvent()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        var bookId = Guid.CreateVersion7();
        var command = new AddBookToCart(userId, bookId, 2);

        var session = Substitute.For<IDocumentSession>();
        var existingProfile = new UserProfile
        {
            Id = userId,
            ShoppingCartItems = new Dictionary<Guid, int> { [bookId] = 3 } // Already has 3
        };
        _ = session.LoadAsync<UserProfile>(userId, Arg.Any<CancellationToken>()).Returns(existingProfile);

        // Act
        await UserCommandHandler.Handle(command, session);

        // Assert - Event always appended, projection merges quantities
        _ = session.Events.Received(1).Append(
            userId,
            Arg.Is<BookAddedToCart>(e => e.BookId == bookId && e.Quantity == 2));
    }

    #endregion

    #region RemoveBookFromCart Tests

    [Test]
    [Category("Unit")]
    public async Task RemoveBookFromCart_WithExistingItem_ShouldAppendEvent()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        var bookId = Guid.CreateVersion7();
        var command = new RemoveBookFromCart(userId, bookId);

        var session = Substitute.For<IDocumentSession>();
        var existingProfile = new UserProfile
        {
            Id = userId,
            ShoppingCartItems = new Dictionary<Guid, int> { [bookId] = 2 }
        };
        _ = session.LoadAsync<UserProfile>(userId, Arg.Any<CancellationToken>()).Returns(existingProfile);

        // Act
        await UserCommandHandler.Handle(command, session);

        // Assert
        _ = session.Events.Received(1).Append(
            userId,
            Arg.Is<BookRemovedFromCart>(e => e.BookId == bookId));
    }

    [Test]
    [Category("Unit")]
    public async Task RemoveBookFromCart_WithoutExistingItem_ShouldNotAppendEvent()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        var bookId = Guid.CreateVersion7();
        var command = new RemoveBookFromCart(userId, bookId);

        var session = Substitute.For<IDocumentSession>();
        var existingProfile = new UserProfile
        {
            Id = userId,
            ShoppingCartItems = [] // Empty cart
        };
        _ = session.LoadAsync<UserProfile>(userId, Arg.Any<CancellationToken>()).Returns(existingProfile);

        // Act
        await UserCommandHandler.Handle(command, session);

        // Assert
        _ = session.Events.DidNotReceive().Append(
            userId,
            Arg.Any<BookRemovedFromCart>());
    }

    [Test]
    [Category("Unit")]
    public async Task RemoveBookFromCart_WithNullProfile_ShouldNotAppendEvent()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        var bookId = Guid.CreateVersion7();
        var command = new RemoveBookFromCart(userId, bookId);

        var session = Substitute.For<IDocumentSession>();
        _ = session.LoadAsync<UserProfile>(userId, Arg.Any<CancellationToken>()).Returns((UserProfile?)null);

        // Act
        await UserCommandHandler.Handle(command, session);

        // Assert
        _ = session.Events.DidNotReceive().Append(
            userId,
            Arg.Any<BookRemovedFromCart>());
    }

    #endregion

    #region UpdateCartItemQuantity Tests

    [Test]
    [Category("Unit")]
    public async Task UpdateCartItemQuantity_WithValidQuantity_ShouldAppendEvent()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        var bookId = Guid.CreateVersion7();
        var command = new UpdateCartItemQuantity(userId, bookId, 5);

        var session = Substitute.For<IDocumentSession>();
        var existingProfile = new UserProfile
        {
            Id = userId,
            ShoppingCartItems = new Dictionary<Guid, int> { [bookId] = 2 }
        };
        _ = session.LoadAsync<UserProfile>(userId, Arg.Any<CancellationToken>()).Returns(existingProfile);

        // Act
        await UserCommandHandler.Handle(command, session);

        // Assert
        _ = session.Events.Received(1).Append(
            userId,
            Arg.Is<CartItemQuantityUpdated>(e => e.BookId == bookId && e.Quantity == 5));
    }

    [Test]
    [Category("Unit")]
    public async Task UpdateCartItemQuantity_WithZeroQuantity_ShouldThrowArgumentException()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        var bookId = Guid.CreateVersion7();
        var command = new UpdateCartItemQuantity(userId, bookId, 0);

        var session = Substitute.For<IDocumentSession>();

        // Act & Assert
        var exception = await Assert.That(async () => await UserCommandHandler.Handle(command, session))
            .Throws<ArgumentException>();
        _ = await Assert.That(exception.Message!).Contains("Quantity must be greater than 0");
    }

    [Test]
    [Category("Unit")]
    public async Task UpdateCartItemQuantity_WithNegativeQuantity_ShouldThrowArgumentException()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        var bookId = Guid.CreateVersion7();
        var command = new UpdateCartItemQuantity(userId, bookId, -3);

        var session = Substitute.For<IDocumentSession>();

        // Act & Assert
        var exception = await Assert.That(async () => await UserCommandHandler.Handle(command, session))
            .Throws<ArgumentException>();
        _ = await Assert.That(exception.Message!).Contains("Quantity must be greater than 0");
    }

    [Test]
    [Category("Unit")]
    public async Task UpdateCartItemQuantity_WithoutExistingItem_ShouldNotAppendEvent()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        var bookId = Guid.CreateVersion7();
        var command = new UpdateCartItemQuantity(userId, bookId, 5);

        var session = Substitute.For<IDocumentSession>();
        var existingProfile = new UserProfile
        {
            Id = userId,
            ShoppingCartItems = [] // Empty cart
        };
        _ = session.LoadAsync<UserProfile>(userId, Arg.Any<CancellationToken>()).Returns(existingProfile);

        // Act
        await UserCommandHandler.Handle(command, session);

        // Assert
        _ = session.Events.DidNotReceive().Append(
            userId,
            Arg.Any<CartItemQuantityUpdated>());
    }

    [Test]
    [Category("Unit")]
    public async Task UpdateCartItemQuantity_WithNullProfile_ShouldNotAppendEvent()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        var bookId = Guid.CreateVersion7();
        var command = new UpdateCartItemQuantity(userId, bookId, 5);

        var session = Substitute.For<IDocumentSession>();
        _ = session.LoadAsync<UserProfile>(userId, Arg.Any<CancellationToken>()).Returns((UserProfile?)null);

        // Act
        await UserCommandHandler.Handle(command, session);

        // Assert
        _ = session.Events.DidNotReceive().Append(
            userId,
            Arg.Any<CartItemQuantityUpdated>());
    }

    #endregion

    #region ClearShoppingCart Tests

    [Test]
    [Category("Unit")]
    public async Task ClearShoppingCart_WithItems_ShouldAppendEvent()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        var command = new ClearShoppingCart(userId);

        var session = Substitute.For<IDocumentSession>();
        var existingProfile = new UserProfile
        {
            Id = userId,
            ShoppingCartItems = new Dictionary<Guid, int>
            {
                [Guid.CreateVersion7()] = 2,
                [Guid.CreateVersion7()] = 1
            }
        };
        _ = session.LoadAsync<UserProfile>(userId, Arg.Any<CancellationToken>()).Returns(existingProfile);

        // Act
        await UserCommandHandler.Handle(command, session);

        // Assert
        _ = session.Events.Received(1).Append(
            userId,
            Arg.Any<ShoppingCartCleared>());
    }

    [Test]
    [Category("Unit")]
    public async Task ClearShoppingCart_WithEmptyCart_ShouldNotAppendEvent()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        var command = new ClearShoppingCart(userId);

        var session = Substitute.For<IDocumentSession>();
        var existingProfile = new UserProfile
        {
            Id = userId,
            ShoppingCartItems = [] // Empty
        };
        _ = session.LoadAsync<UserProfile>(userId, Arg.Any<CancellationToken>()).Returns(existingProfile);

        // Act
        await UserCommandHandler.Handle(command, session);

        // Assert
        _ = session.Events.DidNotReceive().Append(
            userId,
            Arg.Any<ShoppingCartCleared>());
    }

    [Test]
    [Category("Unit")]
    public async Task ClearShoppingCart_WithNullProfile_ShouldNotAppendEvent()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        var command = new ClearShoppingCart(userId);

        var session = Substitute.For<IDocumentSession>();
        _ = session.LoadAsync<UserProfile>(userId, Arg.Any<CancellationToken>()).Returns((UserProfile?)null);

        // Act
        await UserCommandHandler.Handle(command, session);

        // Assert
        _ = session.Events.DidNotReceive().Append(
            userId,
            Arg.Any<ShoppingCartCleared>());
    }

    #endregion
}
