using System.Text.Json;

namespace BookStore.ApiService.Tests;

/// <summary>
/// Tests to verify JSON serialization standards:
/// - ISO 8601 date/time format
/// - UTC timezone
/// - Enum string serialization
/// - camelCase property naming
/// </summary>
public class JsonSerializationTests
{
    // Static lazy initialization for better performance - shared across all tests
    private static readonly Lazy<JsonSerializerOptions> _options = new(() => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    }, LazyThreadSafetyMode.ExecutionAndPublication);

    [Test]
    [Category("Unit")]
    public async Task DateTimeOffset_Should_Serialize_As_ISO8601_With_UTC()
    {
        // Arrange
        var testObject = new
        {
            Timestamp = new DateTimeOffset(2025, 12, 26, 17, 16, 9, 123, TimeSpan.Zero)
        };

        // Act
        var json = JsonSerializer.Serialize(testObject, _options.Value);

        // Assert
        await Assert.That(json).Contains("\"timestamp\":\"2025-12-26T17:16:09.123+00:00\"");
    }

    [Test]
    [Category("Unit")]
    public async Task DateTimeOffset_UtcNow_Should_End_With_Z_Or_UTC_Offset()
    {
        // Arrange
        var testObject = new { Timestamp = DateTimeOffset.UtcNow };

        // Act
        var json = JsonSerializer.Serialize(testObject, _options.Value);

        // Assert - Should match ISO 8601 format with UTC indicator
        await Assert.That(json).Matches(@"""timestamp"":""(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2}):(\d{2})(\.\d+)?\+00:00""");
    }

    [Test]
    [Category("Unit")]
    public async Task DateOnly_Should_Serialize_As_ISO8601_Date()
    {
        // Arrange
        var testObject = new { PublicationDate = new DateOnly(2008, 8, 1) };

        // Act
        var json = JsonSerializer.Serialize(testObject, _options.Value);

        // Assert
        await Assert.That(json).Contains("\"publicationDate\":\"2008-08-01\"");
    }

    [Test]
    [Category("Unit")]
    public async Task Enum_Should_Serialize_As_String_Not_Integer()
    {
        // Arrange
        var testObject = new { Status = TestStatus.Active };

        // Act
        var json = JsonSerializer.Serialize(testObject, _options.Value);

        // Assert
        await Assert.That(json).Contains("\"status\":\"active\"");  // camelCase enum value
        await Assert.That(json).DoesNotContain("\"status\":0");
    }

    [Test]
    [Category("Unit")]
    public async Task Properties_Should_Use_CamelCase()
    {
        // Arrange
        var testObject = new TestDto
        {
            BookId = Guid.CreateVersion7(),
            BookTitle = "Clean Code",
            PublicationDate = new DateOnly(2008, 8, 1),
            LastModified = DateTimeOffset.UtcNow
        };

        // Act
        var json = JsonSerializer.Serialize(testObject, _options.Value);

        // Assert
        await Assert.That(json).Contains("\"bookId\":");
        await Assert.That(json).Contains("\"bookTitle\":");
        await Assert.That(json).Contains("\"publicationDate\":");
        await Assert.That(json).Contains("\"lastModified\":");
        
        // Should NOT contain PascalCase
        await Assert.That(json).DoesNotContain("\"BookId\":");
        await Assert.That(json).DoesNotContain("\"BookTitle\":");
    }

    [Test]
    [Category("Unit")]
    public async Task Complex_Object_Should_Follow_All_Standards()
    {
        // Arrange
        var testObject = new TestDto
        {
            BookId = new Guid("018d5e4a-7b2c-7000-8000-123456789abc"),
            BookTitle = "Clean Code",
            PublicationDate = new DateOnly(2008, 8, 1),
            LastModified = new DateTimeOffset(2025, 12, 26, 17, 16, 9, TimeSpan.Zero),
            Status = TestStatus.Active
        };

        // Act
        var json = JsonSerializer.Serialize(testObject, _options.Value);

        // Assert
        // camelCase properties
        await Assert.That(json).Contains("\"bookId\":");
        await Assert.That(json).Contains("\"bookTitle\":");
        
        // ISO 8601 dates
        await Assert.That(json).Contains("\"publicationDate\":\"2008-08-01\"");
        await Assert.That(json).Contains("\"lastModified\":\"2025-12-26T17:16:09+00:00\"");
        
        // Enum as string
        await Assert.That(json).Contains("\"status\":\"active\"");
    }

    // Test DTOs
    class TestDto
    {
        public Guid BookId { get; set; }
        public string BookTitle { get; set; } = string.Empty;
        public DateOnly PublicationDate { get; set; }
        public DateTimeOffset LastModified { get; set; }
        public TestStatus Status { get; set; }
    }

    enum TestStatus
    {
        Active,
        Inactive,
        Deleted
    }
}
