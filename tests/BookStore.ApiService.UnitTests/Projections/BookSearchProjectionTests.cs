using BookStore.ApiService.Events;
using BookStore.ApiService.Projections;
using BookStore.Shared.Models;
using Marten;
using Marten.Linq;
using NSubstitute;

namespace BookStore.ApiService.UnitTests.Projections;

public class BookSearchProjectionTests
{
    [Test]
    [Category("Unit")]
    public async Task Create_ShouldInitializeProjectionAndLoadDenormalizedData()
    {
        // Arrange
        var id = Guid.CreateVersion7();
        var publisherId = Guid.CreateVersion7();
        var authorId = Guid.CreateVersion7();

        var @event = new BookAdded(
            id,
            "Clean Code",
            "978-0132350884",
            "en",
            new Dictionary<string, BookTranslation> { ["en"] = new("A Handbook of Agile Software Craftsmanship") },
            new PartialDate(2008, 8, 1),
            publisherId,
            [authorId],
            [], // CategoryIds
            [] // Prices
        );

        var session = Substitute.For<IQuerySession>();

        // Mock Publisher lookup
        var publisherList = new List<PublisherProjection> { new() { Id = publisherId, Name = "Prentice Hall" } };
        var publisherQuery = CreateMartenQueryable(publisherList);

        _ = session.Query<PublisherProjection>().Returns(publisherQuery);

        // Mock Author lookup
        var authorList = new List<AuthorProjection> { new() { Id = authorId, Name = "Robert C. Martin" } };
        var authorQuery = CreateMartenQueryable(authorList);

        _ = session.Query<AuthorProjection>().Returns(authorQuery);

        var mockEvent = Substitute.For<JasperFx.Events.IEvent<BookAdded>>();
        _ = mockEvent.Data.Returns(@event);
        _ = mockEvent.Timestamp.Returns(DateTimeOffset.UtcNow);
        _ = mockEvent.Version.Returns(1);

        // Act
        var projection = BookSearchProjection.Create(mockEvent, session);

        // Assert
        _ = await Assert.That(projection.Id).IsEqualTo(id);
        _ = await Assert.That(projection.Title).IsEqualTo("Clean Code");
        _ = await Assert.That(projection.PublisherName).IsEqualTo("Prentice Hall");
        _ = await Assert.That(projection.AuthorNames).IsEqualTo("Robert C. Martin");
        _ = await Assert.That(projection.SearchText).Contains("Clean Code");
        _ = await Assert.That(projection.SearchText).Contains("Prentice Hall");
        _ = await Assert.That(projection.SearchText).Contains("Robert C. Martin");
    }

    [Test]
    [Category("Unit")]
    public async Task Apply_ShouldUpdateProjectionAndReloadDenormalizedData()
    {
        // Arrange
        var projection = new BookSearchProjection { Id = Guid.CreateVersion7(), Title = "Old Title" };

        var publisherId = Guid.CreateVersion7();
        var authorId = Guid.CreateVersion7();

        var @event = new BookUpdated(
            projection.Id,
            "Clean Code Updated",
            "978-0132350884",
            "en",
            new Dictionary<string, BookTranslation> { ["en"] = new("Desc") },
            new PartialDate(2008, 8, 1),
            publisherId,
            [authorId],
            [],
            []
        );

        var session = Substitute.For<IQuerySession>();

        // Mock Publisher lookup
        var publisherList = new List<PublisherProjection> { new() { Id = publisherId, Name = "Prentice Hall" } };
        var publisherQuery = CreateMartenQueryable(publisherList);

        _ = session.Query<PublisherProjection>().Returns(publisherQuery);

        // Mock Author lookup
        var authorList = new List<AuthorProjection> { new() { Id = authorId, Name = "Uncle Bob" } };
        var authorQuery = CreateMartenQueryable(authorList);

        _ = session.Query<AuthorProjection>().Returns(authorQuery);

        var mockEvent = Substitute.For<JasperFx.Events.IEvent<BookUpdated>>();
        _ = mockEvent.Data.Returns(@event);
        _ = mockEvent.Timestamp.Returns(DateTimeOffset.UtcNow);
        _ = mockEvent.Version.Returns(2);

        // Act
        _ = projection.Apply(mockEvent, session);

        // Assert
        _ = await Assert.That(projection.Title).IsEqualTo("Clean Code Updated");
        _ = await Assert.That(projection.PublisherName).IsEqualTo("Prentice Hall");
        _ = await Assert.That(projection.AuthorNames).IsEqualTo("Uncle Bob");
        _ = await Assert.That(projection.SearchText).Contains("Clean Code Updated");
        _ = await Assert.That(projection.SearchText).Contains("Uncle Bob");
    }

    static IMartenQueryable<T> CreateMartenQueryable<T>(IEnumerable<T> source)
    {
        var queryable = source.AsQueryable();
        var mock = Substitute.For<IMartenQueryable<T>>();

        _ = mock.Provider.Returns(queryable.Provider);
        _ = mock.Expression.Returns(queryable.Expression);
        _ = mock.ElementType.Returns(queryable.ElementType);
        _ = mock.GetEnumerator().Returns(queryable.GetEnumerator());

        return mock;
    }

    [Test]
    [Category("Unit")]
    public async Task Apply_BookSaleScheduled_ShouldAddSale()
    {
        // Arrange
        var projection = new BookSearchProjection { Id = Guid.CreateVersion7() };
        var sale = new BookSale(10m, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1));
        var @event = new BookSaleScheduled(projection.Id, sale);

        var mockEvent = Substitute.For<JasperFx.Events.IEvent<BookSaleScheduled>>();
        _ = mockEvent.Data.Returns(@event);
        _ = mockEvent.Timestamp.Returns(DateTimeOffset.UtcNow);
        _ = mockEvent.Version.Returns(2);

        // Act
        projection.Apply(mockEvent);

        // Assert
        _ = await Assert.That(projection.Sales).Count().IsEqualTo(1);
        _ = await Assert.That(projection.Sales[0]).IsEqualTo(sale);
    }

    [Test]
    [Category("Unit")]
    public async Task Apply_BookSaleScheduled_WithExistingOverlap_ShouldReplace()
    {
        // Arrange
        var projection = new BookSearchProjection { Id = Guid.CreateVersion7() };
        var start = DateTimeOffset.UtcNow;
        var existingSale = new BookSale(10m, start, start.AddDays(1));
        projection.Sales.Add(existingSale);

        var newSale = new BookSale(20m, start, start.AddDays(2)); // Same start time, different end/percentage
        var @event = new BookSaleScheduled(projection.Id, newSale);

        var mockEvent = Substitute.For<JasperFx.Events.IEvent<BookSaleScheduled>>();
        _ = mockEvent.Data.Returns(@event);
        _ = mockEvent.Timestamp.Returns(DateTimeOffset.UtcNow);
        _ = mockEvent.Version.Returns(3);

        // Act
        projection.Apply(mockEvent);

        // Assert
        _ = await Assert.That(projection.Sales).Count().IsEqualTo(1);
        _ = await Assert.That(projection.Sales[0].Percentage).IsEqualTo(20m);
        _ = await Assert.That(projection.Sales[0].End).IsEqualTo(newSale.End);
    }

    [Test]
    [Category("Unit")]
    public async Task Apply_BookSaleCancelled_ShouldRemoveSale()
    {
        // Arrange
        var projection = new BookSearchProjection { Id = Guid.CreateVersion7() };
        var start = DateTimeOffset.UtcNow;
        var sale = new BookSale(10m, start, start.AddDays(1));
        projection.Sales.Add(sale);

        var @event = new BookSaleCancelled(projection.Id, start);

        var mockEvent = Substitute.For<JasperFx.Events.IEvent<BookSaleCancelled>>();
        _ = mockEvent.Data.Returns(@event);
        _ = mockEvent.Timestamp.Returns(DateTimeOffset.UtcNow);
        _ = mockEvent.Version.Returns(4);

        // Act
        projection.Apply(mockEvent);

        // Assert
        _ = await Assert.That(projection.Sales).IsEmpty();
    }

    [Test]
    [Category("Unit")]
    public async Task Apply_BookDiscountUpdated_ShouldRecalculateCurrentPrices()
    {
        // Arrange
        var id = Guid.CreateVersion7();
        var projection = new BookSearchProjection
        {
            Id = id,
            Prices = [new PriceEntry("USD", 100m), new PriceEntry("EUR", 80m)],
            CurrentPrices = [new PriceEntry("USD", 100m), new PriceEntry("EUR", 80m)]
        };

        var @event = new BookDiscountUpdated(id, 20m); // 20% discount

        var mockEvent = Substitute.For<JasperFx.Events.IEvent<BookDiscountUpdated>>();
        _ = mockEvent.Data.Returns(@event);
        _ = mockEvent.Timestamp.Returns(DateTimeOffset.UtcNow);
        _ = mockEvent.Version.Returns(2);

        // Act
        projection.Apply(mockEvent);

        // Assert
        _ = await Assert.That(projection.CurrentPrices).Count().IsEqualTo(2);
        _ = await Assert.That(projection.CurrentPrices.First(p => p.Currency == "USD").Value).IsEqualTo(80m);
        _ = await Assert.That(projection.CurrentPrices.First(p => p.Currency == "EUR").Value).IsEqualTo(64m);
    }

    [Test]
    [Category("Unit")]
    public async Task Apply_BookDiscountUpdated_WithZeroDiscount_ShouldResetToOriginalPrices()
    {
        // Arrange
        var id = Guid.CreateVersion7();
        var projection = new BookSearchProjection
        {
            Id = id,
            Prices = [new PriceEntry("USD", 100m)],
            CurrentPrices = [new PriceEntry("USD", 50m)] // Existing 50% discount
        };

        var @event = new BookDiscountUpdated(id, 0m);

        var mockEvent = Substitute.For<JasperFx.Events.IEvent<BookDiscountUpdated>>();
        _ = mockEvent.Data.Returns(@event);
        _ = mockEvent.Timestamp.Returns(DateTimeOffset.UtcNow);
        _ = mockEvent.Version.Returns(2);

        // Act
        projection.Apply(mockEvent);

        // Assert
        _ = await Assert.That(projection.CurrentPrices[0].Value).IsEqualTo(100m);
    }

    [Test]
    [Category("Unit")]
    public async Task Apply_BookUpdated_ShouldPreserveDiscount()
    {
        // Arrange
        var id = Guid.CreateVersion7();
        var projection = new BookSearchProjection
        {
            Id = id,
            Prices = [new PriceEntry("USD", 100m)],
            CurrentPrices = [new PriceEntry("USD", 80m)],
            DiscountPercentage = 20m
        };

        var @event = new BookUpdated(
            id,
            "Updated Title",
            "1234567890",
            "en",
            new Dictionary<string, BookTranslation> { ["en"] = new BookTranslation("Desc") },
            null,
            null,
            [],
            [],
            new Dictionary<string, decimal> { ["USD"] = 200m } // New base price
        );

        var mockEvent = Substitute.For<JasperFx.Events.IEvent<BookUpdated>>();
        _ = mockEvent.Data.Returns(@event);
        _ = mockEvent.Timestamp.Returns(DateTimeOffset.UtcNow);
        _ = mockEvent.Version.Returns(2);

        // Act
        _ = projection.Apply(mockEvent, Substitute.For<IQuerySession>());

        // Assert
        _ = await Assert.That(projection.DiscountPercentage).IsEqualTo(20m);
        _ = await Assert.That(projection.Prices[0].Value).IsEqualTo(200m);
        _ = await Assert.That(projection.CurrentPrices[0].Value).IsEqualTo(160m); // 200 * 0.8
    }
}
