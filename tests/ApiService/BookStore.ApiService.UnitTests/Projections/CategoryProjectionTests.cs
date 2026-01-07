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
        var id = Guid.NewGuid();
        var timestamp = DateTimeOffset.UtcNow;
        var @event = new CategoryAdded(
            id,
            new Dictionary<string, CategoryTranslation>
            {
                ["en"] = new("Technology", "Tech books")
            },
            timestamp
        );

        // Act
        var projection = CategoryProjection.Create(@event);

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
            Id = Guid.NewGuid(),
            Names = new Dictionary<string, string> { ["en"] = "Old Tech" }
        };

        var timestamp = DateTimeOffset.UtcNow;
        var @event = new CategoryUpdated(
            projection.Id,
            new Dictionary<string, CategoryTranslation>
            {
                ["en"] = new("New Tech", "New Desc"),
                ["pt"] = new("Tecnologia", "Desc")
            },
            timestamp
        );

        // Act
        projection.Apply(@event);

        // Assert
        _ = await Assert.That(projection.LastModified).IsEqualTo(timestamp);
        _ = await Assert.That(projection.Names).Count().IsEqualTo(2);
        _ = await Assert.That(projection.Names["en"]).IsEqualTo("New Tech");
        _ = await Assert.That(projection.Names["pt"]).IsEqualTo("Tecnologia");
    }
}
