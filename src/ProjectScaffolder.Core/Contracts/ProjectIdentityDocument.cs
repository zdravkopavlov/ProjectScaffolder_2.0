namespace ProjectScaffolder.Core.Contracts;

public sealed record ProjectIdentityDocument
{
    public required int SchemaVersion { get; init; }

    public required Guid ProjectId { get; init; }

    public required DateTimeOffset CreatedAtUtc { get; init; }

    public required string ProjectName { get; init; }

    public required ProjectTemplateProvenance Template { get; init; }

    public required GeneratorProvenance Generator { get; init; }
}

public sealed record ProjectTemplateProvenance
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public required int Version { get; init; }
}

public sealed record GeneratorProvenance
{
    public required string Name { get; init; }

    public required string Version { get; init; }
}
