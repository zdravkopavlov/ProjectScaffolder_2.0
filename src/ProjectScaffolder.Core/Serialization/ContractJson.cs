using System.Text.Json;
using System.Text.Json.Serialization;
using ProjectScaffolder.Core.Contracts;

namespace ProjectScaffolder.Core.Serialization;

public static class ContractJson
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateOptions();

    public static JsonSerializerOptions Options => new(SerializerOptions);

    public static TemplateDocument DeserializeTemplate(string json) =>
        Deserialize<TemplateDocument>(json);

    public static ProjectIdentityDocument DeserializeProjectIdentity(string json) =>
        Deserialize<ProjectIdentityDocument>(json);

    public static TemplateLibraryDocument DeserializeTemplateLibrary(string json) =>
        Deserialize<TemplateLibraryDocument>(json);

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, SerializerOptions) + Environment.NewLine;

    public static T Deserialize<T>(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return JsonSerializer.Deserialize<T>(json, SerializerOptions)
            ?? throw new JsonException($"The JSON document did not contain a {typeof(T).Name} value.");
    }

    private static JsonSerializerOptions CreateOptions()
    {
        JsonSerializerOptions options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            RespectNullableAnnotations = true,
            RespectRequiredConstructorParameters = true,
            AllowOutOfOrderMetadataProperties = true,
        };

        options.Converters.Add(new UtcDateTimeOffsetJsonConverter());
        options.Converters.Add(new ExactStringEnumConverter<FileCollisionPolicy>(new Dictionary<string, FileCollisionPolicy>(StringComparer.Ordinal)
        {
            ["skip"] = FileCollisionPolicy.Skip,
            ["overwrite"] = FileCollisionPolicy.Overwrite,
            ["fail"] = FileCollisionPolicy.Fail,
        }));
        options.Converters.Add(new ExactStringEnumConverter<TextFileEncodingKind>(new Dictionary<string, TextFileEncodingKind>(StringComparer.Ordinal)
        {
            ["utf-8"] = TextFileEncodingKind.Utf8,
            ["utf-8-bom"] = TextFileEncodingKind.Utf8Bom,
            ["utf-16-le"] = TextFileEncodingKind.Utf16LittleEndian,
            ["windows-1252"] = TextFileEncodingKind.Windows1252,
        }));
        options.Converters.Add(new ExactStringEnumConverter<LineEndingKind>(new Dictionary<string, LineEndingKind>(StringComparer.Ordinal)
        {
            ["crlf"] = LineEndingKind.CrLf,
            ["lf"] = LineEndingKind.Lf,
        }));

        return options;
    }
}
