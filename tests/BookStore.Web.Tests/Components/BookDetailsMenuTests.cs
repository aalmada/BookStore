using System.Net;
using AngleSharp.Dom;
using Bogus;
using BookStore.Client;
using BookStore.Client.Services;
using BookStore.Shared.Models;
using BookStore.Web.Components.Pages;
using BookStore.Web.Components.Pages.Admin;
using BookStore.Web.Services;
using BookStore.Web.Tests.Infrastructure;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MudBlazor;
using NSubstitute;

namespace BookStore.Web.Tests.Components;

public sealed class BookDetailsMenuTests : BunitTestContext
{
    IBooksClient _booksClient = null!;
    IShoppingCartClient _cartClient = null!;
    IJSRuntime _jsRuntime = null!;
    BookStoreEventsService _eventsService = null!;
    HttpClient _eventsHttpClient = null!;
    BookDto _book = null!;
    IRenderedComponent<MudPopoverProvider> _popoverProvider = null!;
    IRenderedComponent<MudDialogProvider> _dialogProvider = null!;
    BunitAuthorizationContext _authContext = null!;

    [Before(Test)]
    public void Setup()
    {
        _ = Context.JSInterop.SetupVoid("mudPopover.initialize", _ => true).SetVoidResult();
        _ = Context.JSInterop.SetupVoid("mudPopover.dispose", _ => true).SetVoidResult();
        _ = Context.JSInterop.SetupVoid("mudKeyInterceptor.connect", _ => true).SetVoidResult();
        _ = Context.JSInterop.SetupVoid("mudKeyInterceptor.disconnect", _ => true).SetVoidResult();
        _ = Context.JSInterop.SetupVoid("mudElementRef.addOnBlurEvent", _ => true).SetVoidResult();
        _ = Context.JSInterop.SetupVoid("mudElementRef.removeOnBlurEvent", _ => true).SetVoidResult();
        _ = Context.JSInterop.SetupVoid("mudElementRef.focus", _ => true).SetVoidResult();

        _booksClient = Substitute.For<IBooksClient>();
        _cartClient = Substitute.For<IShoppingCartClient>();
        _jsRuntime = Substitute.For<IJSRuntime>();
        _authContext = Context.AddAuthorization();

        SetBook(isDeleted: false);

        _eventsHttpClient = new HttpClient(new FailingSseHandler())
        {
            BaseAddress = new Uri("http://localhost")
        };

        _eventsService = new BookStoreEventsService(
            _eventsHttpClient,
            Substitute.For<ILogger<BookStoreEventsService>>(),
            new ClientContextService())
        {
            RetryDelay = TimeSpan.FromMinutes(5)
        };

        var snackbar = Substitute.For<ISnackbar>();
        var queryInvalidationService = new QueryInvalidationService(Substitute.For<ILogger<QueryInvalidationService>>());
        var catalogService = new CatalogService(_booksClient, snackbar, Substitute.For<ILogger<CatalogService>>());
        var currencyService = new CurrencyService(_jsRuntime);
        var anonymousCartService = new AnonymousCartService(_jsRuntime);

        _ = Context.Services.AddSingleton(_booksClient);
        _ = Context.Services.AddSingleton(_cartClient);
        _ = Context.Services.AddSingleton(_eventsService);
        _ = Context.Services.AddSingleton(queryInvalidationService);
        _ = Context.Services.AddSingleton(catalogService);
        _ = Context.Services.AddSingleton(currencyService);
        _ = Context.Services.AddSingleton(anonymousCartService);
    }

    [After(Test)]
    public async Task CleanupAsync()
    {
        await _eventsService.DisposeAsync();
        _eventsHttpClient.Dispose();
    }

    [Test]
    public async Task BookDetails_AdminAndActiveBook_ShouldShowEditAndDeleteMenuItems()
    {
        // Arrange
        _ = _authContext.SetAuthorized("admin-user");
        _ = _authContext.SetRoles("Admin", "ADMIN");
        SetBook(isDeleted: false);

        // Act
        var cut = RenderBookDetails();
        OpenBookActionsMenu(cut);

        // Assert
        _ = await Assert.That(_popoverProvider.Markup.Contains("Edit", StringComparison.Ordinal)).IsTrue();
        _ = await Assert.That(_popoverProvider.Markup.Contains("Delete", StringComparison.Ordinal)).IsTrue();
        _ = await Assert.That(_popoverProvider.Markup.Contains("Restore", StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    public async Task BookDetails_AdminAndDeletedBook_ShouldShowOnlyRestoreMenuItem()
    {
        // Arrange
        _ = _authContext.SetAuthorized("admin-user");
        _ = _authContext.SetRoles("Admin", "ADMIN");
        SetBook(isDeleted: true);

        // Act
        var cut = RenderBookDetails();
        OpenBookActionsMenu(cut);

        // Assert
        _ = await Assert.That(_popoverProvider.Markup.Contains("Restore", StringComparison.Ordinal)).IsTrue();
        _ = await Assert.That(_popoverProvider.Markup.Contains("Edit", StringComparison.Ordinal)).IsFalse();
        _ = await Assert.That(_popoverProvider.Markup.Contains("Delete", StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    public async Task BookDetails_AdminClickingDelete_ShouldInvokeSoftDeleteOnBooksClient()
    {
        // Arrange
        _ = _authContext.SetAuthorized("admin-user");
        _ = _authContext.SetRoles("Admin", "ADMIN");
        SetBook(isDeleted: false);

        // Act
        var cut = RenderBookDetails();
        OpenBookActionsMenu(cut);

        var deleteMenuItem = FindPopoverMenuItem("Delete");
        deleteMenuItem.Click();

        _dialogProvider.WaitForState(
            () => _dialogProvider.Markup.Contains("Delete Book", StringComparison.Ordinal),
            timeout: TimeSpan.FromSeconds(2));

        var deleteConfirmationButton = _dialogProvider
            .FindAll("button")
            .First(button => string.Equals(button.TextContent.Trim(), "Delete", StringComparison.Ordinal));
        deleteConfirmationButton.Click();

        // Assert
        cut.WaitForAssertion(() =>
            _ = _booksClient.Received(1).SoftDeleteBookAsync(_book.Id, Arg.Any<string?>(), Arg.Any<CancellationToken>()));
    }

    [Test]
    public async Task BookDetails_AdminClickingRestore_ShouldInvokeRestoreOnBooksClient()
    {
        // Arrange
        _ = _authContext.SetAuthorized("admin-user");
        _ = _authContext.SetRoles("Admin", "ADMIN");
        SetBook(isDeleted: true);

        // Act
        var cut = RenderBookDetails();
        OpenBookActionsMenu(cut);

        var restoreMenuItem = FindPopoverMenuItem("Restore");
        restoreMenuItem.Click();

        // Assert
        cut.WaitForAssertion(() =>
            _ = _booksClient.Received(1).RestoreBookAsync(_book.Id, Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()));
    }

    [Test]
    public async Task BookDetails_AdminClickingEdit_ShouldOpenEditDialogAndInvokeGetBookAdminOnBooksClient()
    {
        // Arrange
        _ = _authContext.SetAuthorized("admin-user");
        _ = _authContext.SetRoles("Admin", "ADMIN");
        SetBook(isDeleted: false);

        var adminBook = CreateAdminBook(_book.Id);
        _ = _booksClient.GetBookAdminAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(adminBook);

        var dialogService = Substitute.For<IDialogService>();
        _ = Context.Services.AddSingleton(dialogService);

        // Act
        var cut = RenderBookDetails();
        OpenBookActionsMenu(cut);

        var editMenuItem = FindPopoverMenuItem("Edit");
        editMenuItem.Click();

        // Assert
        cut.WaitForAssertion(() =>
            _ = _booksClient.Received(1).GetBookAdminAsync(_book.Id, Arg.Any<CancellationToken>()));

        cut.WaitForAssertion(() =>
            _ = dialogService.Received(1).ShowAsync<AddBookDialog>(
                "Edit Book",
                Arg.Any<DialogParameters<AddBookDialog>>(),
                Arg.Any<DialogOptions>()));
    }

    [Test]
    public async Task BookDetails_NonAdminUser_ShouldNotSeeEllipsisMenu()
    {
        // Arrange
        _ = _authContext.SetAuthorized("user");
        _ = _authContext.SetRoles("User");

        // Act
        var cut = RenderBookDetails();
        cut.WaitForState(() => cut.Markup.Contains(_book.Title, StringComparison.Ordinal));

        // Assert
        _ = await Assert.That(cut.FindComponents<MudMenu>().Count).IsEqualTo(0);
    }

    [Test]
    public async Task BookDetails_AnonymousUser_ShouldNotSeeEllipsisMenu()
    {
        // Arrange
        _ = _authContext.SetNotAuthorized();

        // Act
        var cut = RenderBookDetails();
        cut.WaitForState(() => cut.Markup.Contains(_book.Title, StringComparison.Ordinal));

        // Assert
        _ = await Assert.That(cut.FindComponents<MudMenu>().Count).IsEqualTo(0);
    }

    sealed class FailingSseHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
    }

    IRenderedComponent<CascadingAuthenticationState> RenderBookDetails()
    {
        _popoverProvider = Context.Render<MudPopoverProvider>();
        _dialogProvider = Context.Render<MudDialogProvider>();

        return Context.Render<CascadingAuthenticationState>(parameters =>
            parameters.AddChildContent<BookDetails>(child => child.Add(x => x.Id, _book.Id)));
    }

    void SetBook(bool isDeleted)
    {
        var faker = new Faker();
        _book = new BookDto(
            Id: Guid.CreateVersion7(),
            Title: faker.Commerce.ProductName(),
            Isbn: faker.Commerce.Ean13(),
            Language: "en",
            LanguageName: "English",
            Description: faker.Lorem.Sentence(),
            PublicationDate: new PartialDate(DateTimeOffset.UtcNow.Year),
            IsPreRelease: false,
            Publisher: null,
            Authors: [],
            Categories: [],
            IsFavorite: false,
            Prices: new Dictionary<string, decimal> { ["GBP"] = faker.Random.Decimal(10, 100) },
            IsDeleted: isDeleted,
            ETag: "\"1\"");

        _ = _booksClient.GetBookAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(_book);
    }

    AdminBookDto CreateAdminBook(Guid bookId)
    {
        var faker = new Faker();
        return new AdminBookDto(
            Id: bookId,
            Title: faker.Commerce.ProductName(),
            Isbn: faker.Commerce.Ean13(),
            Language: "en",
            LanguageName: "English",
            Description: faker.Lorem.Sentence(),
            PublicationDate: new PartialDate(DateTimeOffset.UtcNow.Year),
            IsPreRelease: false,
            Publisher: null,
            Authors: [],
            Categories: [],
            IsFavorite: false,
            LikeCount: 0,
            AverageRating: 0,
            RatingCount: 0,
            UserRating: 0,
            Prices: new Dictionary<string, decimal> { ["GBP"] = faker.Random.Decimal(10, 100) },
            CoverImageUrl: null,
            ActiveSale: null,
            CurrentPrices: [],
            IsDeleted: false,
            Translations: new Dictionary<string, BookTranslationDto>
            {
                ["en"] = new BookTranslationDto(faker.Lorem.Sentence())
            },
            ETag: "\"1\"");
    }

    void OpenBookActionsMenu(IRenderedComponent<CascadingAuthenticationState> cut)
    {
        cut.WaitForState(
            () => cut.Markup.Contains(_book.Title, StringComparison.Ordinal),
            timeout: TimeSpan.FromSeconds(5));

        var menuButton = cut
            .FindComponent<MudMenu>()
            .Find("button");
        menuButton.Click();

        _popoverProvider.WaitForState(
            () => _popoverProvider.Markup.Contains("Edit", StringComparison.Ordinal)
               || _popoverProvider.Markup.Contains("Restore", StringComparison.Ordinal),
            timeout: TimeSpan.FromSeconds(5));
    }

    IElement FindPopoverMenuItem(string label)
    {
        _popoverProvider.WaitForState(
            () => _popoverProvider.FindComponents<MudMenuItem>().Any(c =>
                c.Markup.Contains(label, StringComparison.Ordinal)),
            timeout: TimeSpan.FromSeconds(5));

        return _popoverProvider
            .FindComponents<MudMenuItem>()
            .First(c => c.Markup.Contains(label, StringComparison.Ordinal))
            .Find("div");
    }
}
