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
        var id = Guid.NewGuid();
        var publisherId = Guid.NewGuid();
        var authorId = Guid.NewGuid();

        var @event = new BookAdded(
            id,
            "Clean Code",
            "978-0132350884",
            "en",
            new Dictionary<string, BookTranslation>
            {
                ["en"] = new("A Handbook of Agile Software Craftsmanship")
            },
            new PartialDate(2008, 8, 1),
            publisherId,
            [authorId],
            [], // CategoryIds
            [] // Prices
        );

        var session = Substitute.For<IQuerySession>();

        // Mock Publisher lookup
        var publisherList = new List<PublisherProjection>
        {
            new() { Id = publisherId, Name = "Prentice Hall" }
        };
        var publisherQuery = CreateMartenQueryable(publisherList);

        _ = session.Query<PublisherProjection>().Returns(publisherQuery);

        // Mock Author lookup
        var authorList = new List<AuthorProjection>
        {
            new() { Id = authorId, Name = "Robert C. Martin" }
        };
        var authorQuery = CreateMartenQueryable(authorList);

        _ = session.Query<AuthorProjection>().Returns(authorQuery);

        // Act
        var projection = BookSearchProjection.Create(@event, session);

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
        var projection = new BookSearchProjection
        {
            Id = Guid.NewGuid(),
            Title = "Old Title"
        };

        var publisherId = Guid.NewGuid();
        var authorId = Guid.NewGuid();

        var @event = new BookUpdated(
            projection.Id,
            "Clean Code Updated",
            "978-0132350884",
            "en",
            new Dictionary<string, BookTranslation>
            {
                ["en"] = new("Desc")
            },
            new PartialDate(2008, 8, 1),
            publisherId,
            [authorId],
            [],
            []
        );

        var session = Substitute.For<IQuerySession>();

        // Mock Publisher lookup
        var publisherList = new List<PublisherProjection>
        {
            new() { Id = publisherId, Name = "Prentice Hall" }
        };
        var publisherQuery = CreateMartenQueryable(publisherList);

        _ = session.Query<PublisherProjection>().Returns(publisherQuery);

        // Mock Author lookup
        var authorList = new List<AuthorProjection>
        {
            new() { Id = authorId, Name = "Uncle Bob" }
        };
        var authorQuery = CreateMartenQueryable(authorList);

        _ = session.Query<AuthorProjection>().Returns(authorQuery);

        // Act
        _ = projection.Apply(@event, session);

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
}
