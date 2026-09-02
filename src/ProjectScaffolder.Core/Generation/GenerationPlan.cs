using ProjectScaffolder.Core.Contracts;
using ProjectScaffolder.Core.Variables;

namespace ProjectScaffolder.Core.Generation;

public sealed record GenerationPlan(
    GenerationContext Context,
    IReadOnlyList<GenerationOperation> Operations,
    IReadOnlyDictionary<string, string> SemanticPaths);

public abstract record GenerationOperation(Guid NodeId, string RelativePath, string FullPath);

public sealed record CreateDirectoryOperation(Guid NodeId, string RelativePath, string FullPath, bool Hidden = false)
    : GenerationOperation(NodeId, RelativePath, FullPath);

public abstract record CreateFileOperation(
    Guid NodeId,
    string RelativePath,
    string FullPath,
    FileCollisionPolicy Collision)
    : GenerationOperation(NodeId, RelativePath, FullPath);

public sealed record CreateTextFileOperation(
    Guid NodeId,
    string RelativePath,
    string FullPath,
    FileCollisionPolicy Collision,
    string Content,
    TextFileEncodingKind Encoding,
    LineEndingKind LineEndings)
    : CreateFileOperation(NodeId, RelativePath, FullPath, Collision);

public sealed record CopyAssetFileOperation(
    Guid NodeId,
    string RelativePath,
    string FullPath,
    FileCollisionPolicy Collision,
    string AssetPath,
    string Sha256)
    : CreateFileOperation(NodeId, RelativePath, FullPath, Collision);
