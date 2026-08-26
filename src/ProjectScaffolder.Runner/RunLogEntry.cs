namespace ProjectScaffolder.Runner;

internal sealed record RunLogEntry
{
    public required DateTimeOffset TimestampUtc { get; init; }

    public required string ApplicationVersion { get; init; }

    public Guid? TemplateId { get; init; }

    public int? TemplateVersion { get; init; }

    public string? TargetPath { get; init; }

    public required string Outcome { get; init; }

    public int CreatedDirectories { get; init; }

    public int ReusedDirectories { get; init; }

    public int CreatedFiles { get; init; }

    public int SkippedFiles { get; init; }

    public int OverwrittenFiles { get; init; }

    public bool? RollbackCompleted { get; init; }

    public IReadOnlyList<string>? PathsRequiringInspection { get; init; }

    public string? ErrorType { get; init; }

    public string? ErrorMessage { get; init; }

    public string? ErrorDetails { get; init; }
}
