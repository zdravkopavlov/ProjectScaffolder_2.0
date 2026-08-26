using ProjectScaffolder.Core.Validation;

namespace ProjectScaffolder.Core.Generation;

public sealed class GenerationPlanException : Exception
{
    public GenerationPlanException(IReadOnlyList<ValidationIssue> issues)
        : base("The template could not be converted into a safe generation plan.")
    {
        Issues = issues;
    }

    public IReadOnlyList<ValidationIssue> Issues { get; }
}
