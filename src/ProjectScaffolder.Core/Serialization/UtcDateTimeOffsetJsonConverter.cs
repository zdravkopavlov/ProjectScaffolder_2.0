using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProjectScaffolder.Core.Serialization;

internal sealed class UtcDateTimeOffsetJsonConverter : JsonConverter<DateTimeOffset>
{
    private const string OutputFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string value = reader.GetString() ?? throw new JsonException("Expected a UTC timestamp string.");
        if (!value.EndsWith('Z') ||
            !DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset timestamp))
        {
            throw new JsonException($"Timestamp '{value}' is not a valid UTC timestamp ending in Z.");
        }

        return timestamp.ToUniversalTime();
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToUniversalTime().ToString(OutputFormat, CultureInfo.InvariantCulture));
    }
}
