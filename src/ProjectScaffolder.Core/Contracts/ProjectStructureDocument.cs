namespace ProjectScaffolder.Core.Contracts;

public sealed record ProjectStructureDocument
{
    public required int SchemaVersion { get; init; }

    public required Guid ProjectId { get; init; }

    public required IReadOnlyDictionary<string, string> Paths { get; init; }
}
