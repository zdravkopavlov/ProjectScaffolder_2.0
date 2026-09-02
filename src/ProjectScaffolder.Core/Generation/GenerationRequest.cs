using ProjectScaffolder.Core.Contracts;

namespace ProjectScaffolder.Core.Generation;

public sealed record GenerationRequest
{
    public required TemplateDocument Template { get; init; }

    public required string TemplateDirectory { get; init; }

    public required string TargetDirectory { get; init; }

    public required string GeneratorVersion { get; init; }

    public string? UserName { get; init; }
}

public sealed record GenerationResult
{
    public required Guid ProjectId { get; init; }

    public required bool CreatedProjectIdentity { get; init; }

    public required bool CreatedProjectStructure { get; init; }

    public required int CreatedDirectoryCount { get; init; }

    public required int ReusedDirectoryCount { get; init; }

    public required int CreatedFileCount { get; init; }

    public required int SkippedFileCount { get; init; }

    public required int OverwrittenFileCount { get; init; }
}
