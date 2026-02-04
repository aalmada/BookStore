using BookStore.Client;
using BookStore.Shared.Models;
using BookStore.Web.Services;
using Microsoft.Extensions.Logging;
using MudBlazor;
using NSubstitute;
using TUnit.Core;

namespace BookStore.Web.Tests.Services;

public class CatalogServiceTests
{
    IBooksClient _booksClient = null!;
    ISnackbar _snackbar = null!;
    ILogger<CatalogService> _logger = null!;
    CatalogService _sut = null!;

    BookDto CreateBookDto(Guid id = default, bool isFavorite = false, int userRating = 0) => new(
            Id: id == default ? Guid.NewGuid() : id,
            Title: "Test Book",
            Isbn: "1234567890",
            Language: "en",
            LanguageName: "English",
            Description: "Test Description",
            PublicationDate: null,
            IsPreRelease: false,
            Publisher: null,
            Authors: [],
            Categories: [],
            IsFavorite: isFavorite,
            UserRating: userRating
        );

    [Before(Test)]
    public void Setup()
    {
        _booksClient = Substitute.For<IBooksClient>();
        _snackbar = Substitute.For<ISnackbar>();
        _logger = Substitute.For<ILogger<CatalogService>>();
        _sut = new CatalogService(_booksClient, _snackbar, _logger);
    }

    [Test]
    public async Task ToggleFavoriteAsync_ShouldCallAddAndNotify_WhenNotFavorite()
    {
        // Arrange
        var book = CreateBookDto(isFavorite: false);
        var optimisticResult = false;
        var rollbackResult = false;

        // Act
        await _sut.ToggleFavoriteAsync(book,
            res => optimisticResult = res,
            res => rollbackResult = res);

        // Assert
        _ = await Assert.That(optimisticResult).IsTrue();
        await _booksClient.Received(1).AddBookToFavoritesAsync(book.Id);
        _ = _snackbar.Received(1).Add(Arg.Is<string>(s => s.Contains("Added")), Severity.Success);
    }

    [Test]
    public async Task ToggleFavoriteAsync_ShouldCallRemoveAndNotify_WhenFavorite()
    {
        // Arrange
        var book = CreateBookDto(isFavorite: true);
        var optimisticResult = true;

        // Act
        await _sut.ToggleFavoriteAsync(book,
            res => optimisticResult = res,
            _ => { });

        // Assert
        _ = await Assert.That(optimisticResult).IsFalse();
        await _booksClient.Received(1).RemoveBookFromFavoritesAsync(book.Id);
        _ = _snackbar.Received(1).Add(Arg.Is<string>(s => s.Contains("Removed")), Severity.Success);
    }

    [Test]
    public async Task ToggleFavoriteAsync_ShouldRollbackAndNotify_OnFailure()
    {
        // Arrange
        var book = CreateBookDto(isFavorite: false);
        var rollbackResult = false;
        _ = _booksClient.AddBookToFavoritesAsync(book.Id).Returns(Task.FromException(new Exception("API Error")));

        // Act
        await _sut.ToggleFavoriteAsync(book, _ => { }, res => rollbackResult = res);

        // Assert
        _ = await Assert.That(rollbackResult).IsFalse(); // Original state
        _ = _snackbar.Received(1).Add(Arg.Is<string>(s => s.Contains("Failed")), Severity.Error);
    }

    [Test]
    public async Task RateBookAsync_ShouldCallRateAndNotify_OnSuccess()
    {
        // Arrange
        var book = CreateBookDto(userRating: 0);
        var optimisticRating = 0;

        // Act
        await _sut.RateBookAsync(book, 5, r => optimisticRating = r, _ => { });

        // Assert
        _ = await Assert.That(optimisticRating).IsEqualTo(5);
        await _booksClient.Received(1).RateBookAsync(book.Id, Arg.Is<RateBookRequest>(r => r.Rating == 5));
        _ = _snackbar.Received(1).Add(Arg.Is<string>(s => s.Contains("Rated 5 stars")), Severity.Success);
    }

    [Test]
    public async Task RateBookAsync_ShouldRollback_OnFailure()
    {
        // Arrange
        var book = CreateBookDto(userRating: 2);
        var rollbackRating = 0;
        _ = _booksClient.RateBookAsync(Arg.Any<Guid>(), Arg.Any<RateBookRequest>())
            .Returns(Task.FromException(new Exception("API Error")));

        // Act
        await _sut.RateBookAsync(book, 5, _ => { }, r => rollbackRating = r);

        // Assert
        _ = await Assert.That(rollbackRating).IsEqualTo(2);
        _ = _snackbar.Received(1).Add(Arg.Is<string>(s => s.Contains("Failed")), Severity.Error);
    }

    [Test]
    public async Task RemoveRatingAsync_ShouldCallRemove_OnSuccess()
    {
        // Arrange
        var book = CreateBookDto(userRating: 4);
        var optimisticCalled = false;

        // Act
        await _sut.RemoveRatingAsync(book, () => optimisticCalled = true, _ => { });

        // Assert
        _ = await Assert.That(optimisticCalled).IsTrue();
        await _booksClient.Received(1).RemoveBookRatingAsync(book.Id);
        _ = _snackbar.Received(1).Add(Arg.Is<string>(s => s.Contains("removed")), Severity.Success);
    }

    [Test]
    public async Task RemoveRatingAsync_ShouldNotCallClient_IfRatingIsZero()
    {
        // Arrange
        var book = CreateBookDto(userRating: 0);

        // Act
        await _sut.RemoveRatingAsync(book, () => { }, _ => { });

        // Assert
        await _booksClient.DidNotReceive().RemoveBookRatingAsync(Arg.Any<Guid>());
    }
}
