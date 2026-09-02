using ProjectScaffolder.Core.Contracts;

namespace ProjectScaffolder.Core.Generation;

public sealed record ProjectStructureUpdateRequest
{
    public required TemplateDocument Template { get; init; }

    public required string TargetDirectory { get; init; }

    public string? UserName { get; init; }
}

public sealed record ProjectStructureUpdateResult
{
    public required Guid ProjectId { get; init; }

    public required bool Created { get; init; }

    public required IReadOnlyDictionary<string, string> Paths { get; init; }
}
