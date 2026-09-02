using ProjectScaffolder.Core.Validation;

namespace ProjectScaffolder.Core.Generation;

public sealed class ProjectStructureUpdateException(
    string message,
    IReadOnlyList<ValidationIssue>? issues = null,
    Exception? innerException = null) : Exception(message, innerException)
{
    public IReadOnlyList<ValidationIssue> Issues { get; } = issues ?? [];
}
