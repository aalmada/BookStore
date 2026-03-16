using Bogus;
using BookStore.Shared.Models;
using BookStore.Web.Components.Catalog;
using BookStore.Web.Services;
using BookStore.Web.Tests.Infrastructure;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using NSubstitute;

namespace BookStore.Web.Tests.Components;

public class BookCardTests : BunitTestContext
{
    CurrencyService _currencyService = null!;
    Faker _faker = null!;

    [Before(Test)]
    public void Setup()
    {
        _faker = new Faker();
        _currencyService = new CurrencyService(Substitute.For<IJSRuntime>());

        _ = Context.Services.AddSingleton(_currencyService);
    }

    [Test]
    public async Task BookCard_ShouldRenderTitleAuthorAndPrice()
    {
        var title = _faker.Commerce.ProductName();
        var authorName = _faker.Name.FullName();
        var price = decimal.Parse(_faker.Commerce.Price(10, 100));

        var book = new BookDto(
            Guid.CreateVersion7(),
            title,
            _faker.Commerce.Ean13(),
            "en",
            "English",
            _faker.Lorem.Sentence(),
            null,
            false,
            new PublisherDto(Guid.CreateVersion7(), _faker.Company.CompanyName()),
            [new AuthorDto(Guid.CreateVersion7(), authorName, _faker.Lorem.Paragraph())],
            [new CategoryDto(Guid.CreateVersion7(), _faker.Commerce.Department())],
            false,
            Prices: new Dictionary<string, decimal> { ["USD"] = price });

        var cut = RenderComponent<BookCard>(parameters => parameters
            .Add(p => p.Book, book));

        _ = await Assert.That(cut.Find("h3").TextContent).IsEqualTo(title);
        _ = await Assert.That(cut.Find("p").TextContent).IsEqualTo(authorName);
        _ = await Assert.That(cut.Find(".catalog-price-new").TextContent).IsEqualTo(_currencyService.FormatPrice(price));
    }
}