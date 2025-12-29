using System.Text.Json;
using BookStore.Shared.Infrastructure.Json;
using BookStore.Shared.Models;

namespace BookStore.Shared.Tests;

public class PartialDateTests
{
    [Test]
    [Category("Unit")]
    public async Task Constructor_WithYearOnly_SetsProperties()
    {
        var date = new PartialDate(2023);
        using var scope = Assert.Multiple();
        _ = await Assert.That(date.Year).IsEqualTo(2023);
        _ = await Assert.That(date.Month).IsNull();
        _ = await Assert.That(date.Day).IsNull();
    }

    [Test]
    [Category("Unit")]
    public async Task Constructor_WithYearMonth_SetsProperties()
    {
        var date = new PartialDate(2023, 5);
        using var scope = Assert.Multiple();
        _ = await Assert.That(date.Year).IsEqualTo(2023);
        _ = await Assert.That(date.Month).IsEqualTo(5);
        _ = await Assert.That(date.Day).IsNull();
    }

    [Test]
    [Category("Unit")]
    public async Task Constructor_WithFullDate_SetsProperties()
    {
        var date = new PartialDate(2023, 5, 15);
        using var scope = Assert.Multiple();
        _ = await Assert.That(date.Year).IsEqualTo(2023);
        _ = await Assert.That(date.Month).IsEqualTo(5);
        _ = await Assert.That(date.Day).IsEqualTo(15);
    }

    [Test]
    [Category("Unit")]
    public async Task ValidateYear_ThrowsException_ForInvalidYear()
    {
        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
        {
            _ = new PartialDate(0);
            return Task.CompletedTask;
        });

        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
        {
            _ = new PartialDate(10000);
            return Task.CompletedTask;
        });
    }

    [Test]
    [Category("Unit")]
    public async Task ValidateMonth_ThrowsException_ForInvalidMonth()
    {
        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
        {
            _ = new PartialDate(2023, 0);
            return Task.CompletedTask;
        });

        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
        {
            _ = new PartialDate(2023, 13);
            return Task.CompletedTask;
        });
    }

    [Test]
    [Category("Unit")]
    public async Task ToString_ReturnsCorrectFormat()
    {
        using var scope = Assert.Multiple();
        _ = await Assert.That(new PartialDate(2023).ToString()).IsEqualTo("2023");
        _ = await Assert.That(new PartialDate(2023, 5).ToString()).IsEqualTo("2023-05");
        _ = await Assert.That(new PartialDate(2023, 5, 15).ToString()).IsEqualTo("2023-05-15");
    }

    [Test]
    [Category("Unit")]
    public async Task Serialization_WithYearOnly_ReturnsCorrectJson()
    {
        var date = new PartialDate(2023);
        var json = Serialize(date);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        using var scope = Assert.Multiple();
        _ = await Assert.That(root.GetProperty("year").GetInt32()).IsEqualTo(2023);
        _ = await Assert.That(root.TryGetProperty("month", out var unusedMonth)).IsFalse();
        _ = await Assert.That(root.TryGetProperty("day", out var unusedDay)).IsFalse();
    }

    [Test]
    [Category("Unit")]
    public async Task Serialization_WithFullDate_ReturnsCorrectJson()
    {
        var date = new PartialDate(2023, 5, 15);
        var json = Serialize(date);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        using var scope = Assert.Multiple();
        _ = await Assert.That(root.GetProperty("year").GetInt32()).IsEqualTo(2023);
        _ = await Assert.That(root.GetProperty("month").GetInt32()).IsEqualTo(5);
        _ = await Assert.That(root.GetProperty("day").GetInt32()).IsEqualTo(15);
    }

    [Test]
    [Category("Unit")]
    public async Task Serialization_NullPartialDate_ReturnsNull()
    {
        PartialDate? date = null;
        var json = Serialize(date);
        _ = await Assert.That(json).IsEqualTo("null");
    }

    [Test]
    [Category("Unit")]
    public async Task Serialization_DefaultStruct_ReturnsNull()
    {
        PartialDate? date = new PartialDate(); // default struct
        var json = Serialize(date);
        _ = await Assert.That(json).IsEqualTo("null");
    }

    [Test]
    [Category("Unit")]
    public async Task Deserialization_WithYearOnly_ReturnsCorrectObject()
    {
        var json = """{"year": 2023, "month": null, "day": null}""";
        var date = Deserialize(json);

        _ = await Assert.That(date).IsNotNull();
        using var scope = Assert.Multiple();
        _ = await Assert.That(date!.Value.Year).IsEqualTo(2023);
        _ = await Assert.That(date!.Value.Month).IsNull();
        _ = await Assert.That(date!.Value.Day).IsNull();
    }

    [Test]
    [Category("Unit")]
    public async Task Deserialization_WithPascalCase_ReturnsCorrectObject()
    {
        var json = """{"Year": 2023, "Month": 5, "Day": 15}""";
        var date = Deserialize(json);

        _ = await Assert.That(date).IsNotNull();
        using var scope = Assert.Multiple();
        _ = await Assert.That(date!.Value.Year).IsEqualTo(2023);
        _ = await Assert.That(date!.Value.Month).IsEqualTo(5);
        _ = await Assert.That(date!.Value.Day).IsEqualTo(15);
    }

    [Test]
    [Category("Unit")]
    public async Task Deserialization_Null_ReturnsNull()
    {
        var json = "null";
        var date = Deserialize(json);
        _ = await Assert.That(date).IsNull();
    }

    [Test]
    [Category("Unit")]
    public async Task Deserialization_InvalidJson_ThrowsException()
    {
        var json = """{"year": "invalid"}""";
        _ = await Assert.ThrowsAsync<JsonException>(() =>
        {
            _ = Deserialize(json);
            return Task.CompletedTask;
        });
    }

    private string Serialize(PartialDate? date)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new PartialDateJsonConverter() }
        };
        return JsonSerializer.Serialize(date, options);
    }

    private PartialDate? Deserialize(string json)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new PartialDateJsonConverter() }
        };
        return JsonSerializer.Deserialize<PartialDate?>(json, options);
    }
}
