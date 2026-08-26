using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProjectScaffolder.Core.Serialization;

internal sealed class ExactStringEnumConverter<TEnum> : JsonConverter<TEnum>
    where TEnum : struct, Enum
{
    private readonly IReadOnlyDictionary<string, TEnum> _fromJson;
    private readonly Dictionary<TEnum, string> _toJson;

    public ExactStringEnumConverter(IReadOnlyDictionary<string, TEnum> values)
    {
        _fromJson = values;
        _toJson = values.ToDictionary(pair => pair.Value, pair => pair.Key);
    }

    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException($"Expected a string value for {typeof(TEnum).Name}.");
        }

        string value = reader.GetString() ?? string.Empty;
        if (!_fromJson.TryGetValue(value, out TEnum result))
        {
            throw new JsonException($"Unsupported {typeof(TEnum).Name} value '{value}'.");
        }

        return result;
    }

    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
    {
        if (!_toJson.TryGetValue(value, out string? text))
        {
            throw new JsonException($"Unsupported {typeof(TEnum).Name} value '{value}'.");
        }

        writer.WriteStringValue(text);
    }
}
