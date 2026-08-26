namespace ProjectScaffolder.Core.Contracts;

public sealed record TemplateLibraryDocument
{
    public required int SchemaVersion { get; init; }

    public required IReadOnlyList<Guid> TemplateOrder { get; init; }

    public required IReadOnlyList<Guid> ExplorerTemplateIds { get; init; }
}
