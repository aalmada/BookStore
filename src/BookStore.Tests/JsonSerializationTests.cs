using System.Text.Json;
using Xunit;

namespace BookStore.Tests;

/// <summary>
/// Tests to verify JSON serialization standards:
/// - ISO 8601 date/time format
/// - UTC timezone
/// - Enum string serialization
/// - camelCase property naming
/// </summary>
public class JsonSerializationTests
{
    readonly JsonSerializerOptions _options;

    public JsonSerializationTests()
    {
        // Use the same options as the API
        _options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };
    }

    [Fact]
    public void DateTimeOffset_Should_Serialize_As_ISO8601_With_UTC()
    {
        // Arrange
        var testObject = new
        {
            Timestamp = new DateTimeOffset(2025, 12, 26, 17, 16, 9, 123, TimeSpan.Zero)
        };

        // Act
        var json = JsonSerializer.Serialize(testObject, _options);

        // Assert
        Assert.Contains("\"timestamp\":\"2025-12-26T17:16:09.123+00:00\"", json);
    }

    [Fact]
    public void DateTimeOffset_UtcNow_Should_End_With_Z_Or_UTC_Offset()
    {
        // Arrange
        var testObject = new { Timestamp = DateTimeOffset.UtcNow };

        // Act
        var json = JsonSerializer.Serialize(testObject, _options);

        // Assert - Should match ISO 8601 format with UTC indicator
        Assert.Matches(@"""timestamp"":""(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2}):(\d{2})(\.\d+)?\+00:00""", json);
    }

    [Fact]
    public void DateOnly_Should_Serialize_As_ISO8601_Date()
    {
        // Arrange
        var testObject = new { PublicationDate = new DateOnly(2008, 8, 1) };

        // Act
        var json = JsonSerializer.Serialize(testObject, _options);

        // Assert
        Assert.Contains("\"publicationDate\":\"2008-08-01\"", json);
    }

    [Fact]
    public void Enum_Should_Serialize_As_String_Not_Integer()
    {
        // Arrange
        var testObject = new { Status = TestStatus.Active };

        // Act
        var json = JsonSerializer.Serialize(testObject, _options);

        // Assert
        Assert.Contains("\"status\":\"active\"", json);  // camelCase enum value
        Assert.DoesNotContain("\"status\":0", json);
    }

    [Fact]
    public void Properties_Should_Use_CamelCase()
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
        var json = JsonSerializer.Serialize(testObject, _options);

        // Assert
        Assert.Contains("\"bookId\":", json);
        Assert.Contains("\"bookTitle\":", json);
        Assert.Contains("\"publicationDate\":", json);
        Assert.Contains("\"lastModified\":", json);
        
        // Should NOT contain PascalCase
        Assert.DoesNotContain("\"BookId\":", json);
        Assert.DoesNotContain("\"BookTitle\":", json);
    }

    [Fact]
    public void Complex_Object_Should_Follow_All_Standards()
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
        var json = JsonSerializer.Serialize(testObject, _options);

        // Assert
        // camelCase properties
        Assert.Contains("\"bookId\":", json);
        Assert.Contains("\"bookTitle\":", json);
        
        // ISO 8601 dates
        Assert.Contains("\"publicationDate\":\"2008-08-01\"", json);
        Assert.Contains("\"lastModified\":\"2025-12-26T17:16:09+00:00\"", json);
        
        // Enum as string
        Assert.Contains("\"status\":\"active\"", json);
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
