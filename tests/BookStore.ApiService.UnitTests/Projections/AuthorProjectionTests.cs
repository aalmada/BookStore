using BookStore.ApiService.Events;
using BookStore.ApiService.Projections;
using BookStore.Shared.Models;

namespace BookStore.ApiService.UnitTests.Projections;

public class AuthorProjectionTests
{
    [Test]
    [Category("Unit")]
    public async Task Create_ShouldInitializeProjectionFromEvent()
    {
        // Arrange
        var id = Guid.NewGuid();
        var timestamp = DateTimeOffset.UtcNow;
        var @event = new AuthorAdded(
            id,
            "Robert C. Martin",
            new Dictionary<string, AuthorTranslation> { ["en"] = new("Uncle Bob") },
            timestamp
        );

        var mockEvent = Substitute.For<JasperFx.Events.IEvent<AuthorAdded>>();
        mockEvent.Data.Returns(@event);
        mockEvent.Timestamp.Returns(timestamp);
        mockEvent.Version.Returns(1);

        // Act
        var projection = AuthorProjection.Create(mockEvent);

        // Assert
        _ = await Assert.That(projection.Id).IsEqualTo(id);
        _ = await Assert.That(projection.Name).IsEqualTo("Robert C. Martin");
        _ = await Assert.That(projection.LastModified).IsEqualTo(timestamp);
        _ = await Assert.That(projection.Biographies).ContainsKey("en");
        _ = await Assert.That(projection.Biographies["en"]).IsEqualTo("Uncle Bob");
    }

    [Test]
    [Category("Unit")]
    public async Task Apply_ShouldUpdateProjectionFromEvent()
    {
        // Arrange
        var projection = new AuthorProjection
        {
            Id = Guid.NewGuid(),
            Name = "Old Name",
            Biographies = new Dictionary<string, string> { ["en"] = "Old Bio" }
        };

        var timestamp = DateTimeOffset.UtcNow;
        var @event = new AuthorUpdated(
            projection.Id,
            "New Name",
            new Dictionary<string, AuthorTranslation> { ["en"] = new("New Bio"), ["pt"] = new("Nova Bio") },
            timestamp
        );

        var mockEvent = Substitute.For<JasperFx.Events.IEvent<AuthorUpdated>>();
        mockEvent.Data.Returns(@event);
        mockEvent.Timestamp.Returns(timestamp);
        mockEvent.Version.Returns(2);

        // Act
        projection.Apply(mockEvent);

        // Assert
        _ = await Assert.That(projection.Name).IsEqualTo("New Name");
        _ = await Assert.That(projection.LastModified).IsEqualTo(timestamp);
        _ = await Assert.That(projection.Biographies).Count().IsEqualTo(2);
        _ = await Assert.That(projection.Biographies["en"]).IsEqualTo("New Bio");
        _ = await Assert.That(projection.Biographies["pt"]).IsEqualTo("Nova Bio");
    }
}
