using ProjectScaffolder.Core.Validation;

namespace ProjectScaffolder.Core.Generation;

public sealed class ProjectGenerationException : Exception
{
    public ProjectGenerationException(
        string message,
        IReadOnlyList<ValidationIssue>? issues = null,
        RollbackResult? rollback = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Issues = issues ?? [];
        Rollback = rollback;
    }

    public IReadOnlyList<ValidationIssue> Issues { get; }

    public RollbackResult? Rollback { get; }
}

public sealed record RollbackResult(bool Completed, IReadOnlyList<string> PathsRequiringInspection);
