using System.Text.Json.Serialization;

namespace ProjectScaffolder.Core.Contracts;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(FolderNode), "folder")]
[JsonDerivedType(typeof(TextFileNode), "textFile")]
[JsonDerivedType(typeof(AssetFileNode), "assetFile")]
public abstract record TemplateNode
{
    public required Guid NodeId { get; init; }

    public required string Name { get; init; }
}

public sealed record FolderNode : TemplateNode
{
    public bool Hidden { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Role { get; init; }

    public required IReadOnlyList<TemplateNode> Children { get; init; }
}

public abstract record FileNode : TemplateNode
{
    public FileCollisionPolicy? Collision { get; init; }
}

public sealed record TextFileNode : FileNode
{
    public required TextFileEncodingKind Encoding { get; init; }

    public required LineEndingKind LineEndings { get; init; }

    public required string Content { get; init; }
}

public sealed record AssetFileNode : FileNode
{
    public required AssetReference Asset { get; init; }
}

public sealed record AssetReference
{
    public required string Path { get; init; }

    public required string Sha256 { get; init; }
}
