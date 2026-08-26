namespace ProjectScaffolder.Core.Contracts;

public sealed record TemplateDocument
{
    public required int SchemaVersion { get; init; }

    public required Guid TemplateId { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public required int TemplateVersion { get; init; }

    public required DateTimeOffset CreatedAtUtc { get; init; }

    public required DateTimeOffset ModifiedAtUtc { get; init; }

    public required FileCollisionPolicy DefaultFileCollision { get; init; }

    public required IReadOnlyList<TemplateNode> Nodes { get; init; }
}
