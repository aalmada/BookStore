using BookStore.Client;
using BookStore.Client.Services;
using BookStore.Shared.Models;
using BookStore.Web.Services;
using Microsoft.Extensions.Logging;
using MudBlazor;
using NSubstitute;
using TUnit.Core;

namespace BookStore.Web.Tests.Services;

public class CatalogServiceTests
{
    static readonly Uri TestBaseAddress = new("http://localhost");

    IBooksClient _booksClient = null!;
    ISnackbar _snackbar = null!;
    ILogger<CatalogService> _logger = null!;
    CatalogService _sut = null!;

    BookDto CreateBookDto(Guid id = default, bool isFavorite = false, int userRating = 0) => new(
        Id: id == default ? Guid.CreateVersion7() : id,
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
        var query = await CreateBookQueryAsync(book);

        // Act
        await _sut.ToggleFavoriteAsync(book, query);

        // Assert
        _ = await Assert.That(query.Data?.IsFavorite).IsTrue();
        await _booksClient.Received(1).AddBookToFavoritesAsync(book.Id);
        _ = _snackbar.Received(1).Add(Arg.Is<string>(s => s.Contains("Added")), Severity.Success);

        query.Dispose();
    }

    [Test]
    public async Task ToggleFavoriteAsync_ShouldCallRemoveAndNotify_WhenFavorite()
    {
        // Arrange
        var book = CreateBookDto(isFavorite: true);
        var query = await CreateBookQueryAsync(book);

        // Act
        await _sut.ToggleFavoriteAsync(book, query);

        // Assert
        _ = await Assert.That(query.Data?.IsFavorite).IsFalse();
        await _booksClient.Received(1).RemoveBookFromFavoritesAsync(book.Id);
        _ = _snackbar.Received(1).Add(Arg.Is<string>(s => s.Contains("Removed")), Severity.Success);

        query.Dispose();
    }

    [Test]
    public async Task ToggleFavoriteAsync_ShouldRollbackAndNotify_OnFailure()
    {
        // Arrange
        var book = CreateBookDto(isFavorite: false);
        var query = await CreateBookQueryAsync(book);
        _ = _booksClient.AddBookToFavoritesAsync(book.Id).Returns(Task.FromException(new Exception("API Error")));

        // Act
        await _sut.ToggleFavoriteAsync(book, query);

        // Assert
        _ = await Assert.That(query.Data?.IsFavorite).IsFalse();
        _ = _snackbar.Received(1).Add(Arg.Is<string>(s => s.Contains("Failed")), Severity.Error);

        query.Dispose();
    }

    [Test]
    public async Task RateBookAsync_ShouldCallRateAndNotify_OnSuccess()
    {
        // Arrange
        var book = CreateBookDto(userRating: 0);
        var query = await CreateBookQueryAsync(book);

        // Act
        await _sut.RateBookAsync(book, 5, query);

        // Assert
        _ = await Assert.That(query.Data?.UserRating).IsEqualTo(5);
        await _booksClient.Received(1).RateBookAsync(book.Id, Arg.Is<RateBookRequest>(r => r.Rating == 5));
        _ = _snackbar.Received(1).Add(Arg.Is<string>(s => s.Contains("Rated 5 stars")), Severity.Success);

        query.Dispose();
    }

    [Test]
    public async Task RateBookAsync_ShouldRollback_OnFailure()
    {
        // Arrange
        var book = CreateBookDto(userRating: 2);
        var query = await CreateBookQueryAsync(book);
        _ = _booksClient.RateBookAsync(Arg.Any<Guid>(), Arg.Any<RateBookRequest>())
            .Returns(Task.FromException(new Exception("API Error")));

        // Act
        await _sut.RateBookAsync(book, 5, query);

        // Assert
        _ = await Assert.That(query.Data?.UserRating).IsEqualTo(2);
        _ = _snackbar.Received(1).Add(Arg.Is<string>(s => s.Contains("Failed")), Severity.Error);

        query.Dispose();
    }

    [Test]
    public async Task RemoveRatingAsync_ShouldCallRemove_OnSuccess()
    {
        // Arrange
        var book = CreateBookDto(userRating: 4);
        var query = await CreateBookQueryAsync(book);

        // Act
        await _sut.RemoveRatingAsync(book, query);

        // Assert
        _ = await Assert.That(query.Data?.UserRating).IsEqualTo(0);
        await _booksClient.Received(1).RemoveBookRatingAsync(book.Id);
        _ = _snackbar.Received(1).Add(Arg.Is<string>(s => s.Contains("removed")), Severity.Success);

        query.Dispose();
    }

    [Test]
    public async Task RemoveRatingAsync_ShouldNotCallClient_IfRatingIsZero()
    {
        // Arrange
        var book = CreateBookDto(userRating: 0);
        var query = await CreateBookQueryAsync(book);

        // Act
        await _sut.RemoveRatingAsync(book, query);

        // Assert
        await _booksClient.DidNotReceive().RemoveBookRatingAsync(Arg.Any<Guid>());

        query.Dispose();
    }

    async Task<ReactiveQuery<BookDto?>> CreateBookQueryAsync(BookDto book)
    {
        var eventsService = new BookStoreEventsService(
            new HttpClient { BaseAddress = TestBaseAddress },
            Substitute.For<ILogger<BookStoreEventsService>>(),
            new ClientContextService());

        var invalidationService = new QueryInvalidationService(Substitute.For<ILogger<QueryInvalidationService>>());
        var queryLogger = Substitute.For<ILogger>();

        var query = new ReactiveQuery<BookDto?>(
            queryFn: _ => Task.FromResult<BookDto?>(book),
            eventsService: eventsService,
            invalidationService: invalidationService,
            queryKeys: ["Book"],
            onStateChanged: () => { },
            logger: queryLogger);

        await query.LoadAsync();
        return query;
    }
}
