using System.Text.Json;
using System.Text.Json.Serialization;
using BookStore.Web.Services.Models;

namespace BookStore.Web.Services.Infrastructure.Json;

/// <summary>
/// Custom JSON converter for PartialDate that properly handles nullable values
/// and serializes the struct as an object with year, month, and day properties.
/// </summary>
public class PartialDateJsonConverter : JsonConverter<PartialDate?>
{
    public override PartialDate? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException($"Expected StartObject token but got {reader.TokenType}");
        }

        int? year = null;
        int? month = null;
        int? day = null;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                break;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException($"Expected PropertyName token but got {reader.TokenType}");
            }

            var propertyName = reader.GetString();
            if (!reader.Read())
            {
                throw new JsonException("Unexpected end of JSON while reading property value");
            }

            // Use ReadOnlySpan to avoid string allocations for comparison
            if (propertyName != null && propertyName.AsSpan().Equals("year", StringComparison.OrdinalIgnoreCase))
            {
                if (reader.TokenType == JsonTokenType.Number)
                {
                    year = reader.GetInt32();
                }
                else if (reader.TokenType != JsonTokenType.Null)
                {
                    throw new JsonException($"Expected Number or Null for 'year' but got {reader.TokenType}");
                }
            }
            else if (propertyName != null && propertyName.AsSpan().Equals("month", StringComparison.OrdinalIgnoreCase))
            {
                if (reader.TokenType == JsonTokenType.Number)
                {
                    month = reader.GetInt32();
                }
                else if (reader.TokenType != JsonTokenType.Null)
                {
                    throw new JsonException($"Expected Number or Null for 'month' but got {reader.TokenType}");
                }
            }
            else if (propertyName != null && propertyName.AsSpan().Equals("day", StringComparison.OrdinalIgnoreCase))
            {
                if (reader.TokenType == JsonTokenType.Number)
                {
                    day = reader.GetInt32();
                }
                else if (reader.TokenType != JsonTokenType.Null)
                {
                    throw new JsonException($"Expected Number or Null for 'day' but got {reader.TokenType}");
                }
            }
            // Ignore unknown properties for forward compatibility
        }

        // If year is not provided or is 0, return null
        if (!year.HasValue || year.Value == 0)
        {
            return null;
        }

        // Create PartialDate based on available components
        // Let the PartialDate constructor handle validation
        try
        {
            if (day.HasValue && month.HasValue)
            {
                return new PartialDate(year.Value, month.Value, day.Value);
            }

            if (month.HasValue)
            {
                return new PartialDate(year.Value, month.Value);
            }

            return new PartialDate(year.Value);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            throw new JsonException($"Invalid PartialDate values: year={year}, month={month}, day={day}", ex);
        }
    }

    public override void Write(Utf8JsonWriter writer, PartialDate? value, JsonSerializerOptions options)
    {
        if (!value.HasValue)
        {
            writer.WriteNullValue();
            return;
        }

        var partialDate = value.Value;

        // Check if this is a default/uninitialized struct (year == 0)
        if (partialDate.Year == 0)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartObject();

        // Use naming policy if configured, otherwise use PascalCase
        var namingPolicy = options.PropertyNamingPolicy;

        // Write year (always present)
        var yearPropertyName = namingPolicy?.ConvertName("Year") ?? "Year";
        writer.WriteNumber(yearPropertyName, partialDate.Year);

        // Write month (nullable)
        if (partialDate.Month.HasValue)
        {
            var monthPropertyName = namingPolicy?.ConvertName("Month") ?? "Month";
            writer.WriteNumber(monthPropertyName, partialDate.Month.Value);
        }

        // Write day (nullable)
        if (partialDate.Day.HasValue)
        {
            var dayPropertyName = namingPolicy?.ConvertName("Day") ?? "Day";
            writer.WriteNumber(dayPropertyName, partialDate.Day.Value);
        }

        writer.WriteEndObject();
    }
}
