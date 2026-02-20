using BookStore.ApiService.Events;
using BookStore.ApiService.Projections;
using BookStore.Shared.Models;

namespace BookStore.ApiService.UnitTests.Projections;

public class CategoryProjectionTests
{
    [Test]
    [Category("Unit")]
    public async Task Create_ShouldInitializeProjectionFromEvent()
    {
        // Arrange
        var id = Guid.CreateVersion7();
        var timestamp = DateTimeOffset.UtcNow;
        var @event = new CategoryAdded(
            id,
            new Dictionary<string, CategoryTranslation> { ["en"] = new("Technology") },
            timestamp
        );

        var mockEvent = Substitute.For<JasperFx.Events.IEvent<CategoryAdded>>();
        _ = mockEvent.Data.Returns(@event);
        _ = mockEvent.Timestamp.Returns(timestamp);
        _ = mockEvent.Version.Returns(1);

        // Act
        var projection = CategoryProjection.Create(mockEvent);

        // Assert
        _ = await Assert.That(projection.Id).IsEqualTo(id);
        _ = await Assert.That(projection.LastModified).IsEqualTo(timestamp);
        _ = await Assert.That(projection.Names).ContainsKey("en");
        _ = await Assert.That(projection.Names["en"]).IsEqualTo("Technology");
    }

    [Test]
    [Category("Unit")]
    public async Task Apply_ShouldUpdateProjectionFromEvent()
    {
        // Arrange
        var projection = new CategoryProjection
        {
            Id = Guid.CreateVersion7(),
            Names = new Dictionary<string, string> { ["en"] = "Old Tech" }
        };

        var timestamp = DateTimeOffset.UtcNow;
        var @event = new CategoryUpdated(
            projection.Id,
            new Dictionary<string, CategoryTranslation> { ["en"] = new("New Tech"), ["pt"] = new("Tecnologia") },
            timestamp
        );

        var mockEvent = Substitute.For<JasperFx.Events.IEvent<CategoryUpdated>>();
        _ = mockEvent.Data.Returns(@event);
        _ = mockEvent.Timestamp.Returns(timestamp);
        _ = mockEvent.Version.Returns(2);

        // Act
        projection.Apply(mockEvent);

        // Assert
        _ = await Assert.That(projection.LastModified).IsEqualTo(timestamp);
        _ = await Assert.That(projection.Names).Count().IsEqualTo(2);
        _ = await Assert.That(projection.Names["en"]).IsEqualTo("New Tech");
        _ = await Assert.That(projection.Names["pt"]).IsEqualTo("Tecnologia");
    }
}
