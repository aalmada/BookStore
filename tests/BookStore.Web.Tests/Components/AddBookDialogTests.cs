using Bogus;
using BookStore.Client;
using BookStore.Shared.Models;
using BookStore.Web.Components.Pages.Admin;
using BookStore.Web.Services;
using BookStore.Web.Tests.Infrastructure;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using MudBlazor;
using NSubstitute;

namespace BookStore.Web.Tests.Components;

public class AddBookDialogTests : BunitTestContext
{
    IBooksClient _booksClient = null!;
    IConfigurationClient _configurationClient = null!;
    IAuthorsClient _authorsClient = null!;
    ICategoriesClient _categoriesClient = null!;
    IPublishersClient _publishersClient = null!;
    IJSRuntime _jsRuntime = null!;
    CurrencyService _currencyService = null!;
    LanguageService _languageService = null!;

    [Before(Test)]
    public void Setup()
    {
        _booksClient = Substitute.For<IBooksClient>();
        _configurationClient = Substitute.For<IConfigurationClient>();
        _authorsClient = Substitute.For<IAuthorsClient>();
        _categoriesClient = Substitute.For<ICategoriesClient>();
        _publishersClient = Substitute.For<IPublishersClient>();
        _jsRuntime = Substitute.For<IJSRuntime>();

        _currencyService = new CurrencyService(_jsRuntime);
        _languageService = new LanguageService(_configurationClient);

        _ = _configurationClient.GetCurrencyConfigAsync(Arg.Any<CancellationToken>())
            .Returns(new CurrencyConfigDto("GBP", ["GBP", "USD"]));
        _ = _configurationClient.GetLocalizationConfigAsync()
            .Returns(new LocalizationConfigDto("en-US", ["en-US", "pt-PT"]));
        _ = _booksClient.UpdateBookAsync(Arg.Any<Guid>(), Arg.Any<UpdateBookRequest>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _ = Context.Services.AddSingleton(_booksClient);
        _ = Context.Services.AddSingleton(_configurationClient);
        _ = Context.Services.AddSingleton(_authorsClient);
        _ = Context.Services.AddSingleton(_categoriesClient);
        _ = Context.Services.AddSingleton(_publishersClient);
        _ = Context.Services.AddSingleton(_currencyService);
        _ = Context.Services.AddSingleton(_languageService);
    }

    [Test]
    public async Task AddBookDialog_EditMode_ShouldPrepopulateGbpPrice_AndSaveTypedPrices()
    {
        // Arrange
        var faker = new Faker();
        var bookId = Guid.CreateVersion7();
        var existingGbpPrice = 12.34m;
        var model = new CreateBookRequest
        {
            Title = faker.Commerce.ProductName(),
            Isbn = faker.Commerce.Ean13(),
            Language = "en-US",
            PublicationDate = new PartialDate(faker.Date.Past(10).Year),
            Translations = new Dictionary<string, BookTranslationDto>
            {
                ["en-US"] = new(faker.Lorem.Sentence())
            },
            AuthorIds = [],
            CategoryIds = [],
            Prices = new Dictionary<string, decimal>
            {
                ["GBP"] = existingGbpPrice,
                ["USD"] = 19.99m
            }
        };

        var parameters = new DialogParameters<AddBookDialog>
        {
            { p => p.Model, model },
            { p => p.IsEdit, true },
            { p => p.BookId, bookId },
            { p => p.ETag, "\"etag-1\"" },
            { p => p.InitialAuthors, [] },
            { p => p.InitialCategories, [] },
            { p => p.InitialPublisher, null }
        };

        var dialogProvider = RenderComponent<MudDialogProvider>();
        var dialogService = Context.Services.GetRequiredService<IDialogService>();

        await dialogProvider.InvokeAsync(async () =>
        {
            _ = await dialogService.ShowAsync<AddBookDialog>("Edit Book", parameters);
        });

        dialogProvider.WaitForState(() => dialogProvider.Markup.Contains("Edit Book", StringComparison.Ordinal));

        // Act
        var form = dialogProvider.FindComponent<MudForm>();
        await dialogProvider.InvokeAsync(form.Instance.ValidateAsync);

        var updateButton = dialogProvider.FindAll("button").Last(button => button.TextContent.Trim() == "Update");
        await updateButton.ClickAsync(new MouseEventArgs());

        // Assert save path
        await _booksClient.Received(1).UpdateBookAsync(
            bookId,
            Arg.Is<UpdateBookRequest>(request =>
                request.Prices != null &&
                request.Prices.ContainsKey("GBP") &&
                request.Prices["GBP"] == existingGbpPrice &&
                !request.AdditionalProperties.ContainsKey("prices")),
            "\"etag-1\"",
            Arg.Any<CancellationToken>());
    }
}
