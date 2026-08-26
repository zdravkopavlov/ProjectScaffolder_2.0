using ProjectScaffolder.Core.Validation;

namespace ProjectScaffolder.Core.Serialization;

public sealed class ContractValidationException : Exception
{
    public ContractValidationException(string message, IReadOnlyList<ValidationIssue> issues)
        : base(message)
    {
        Issues = issues;
    }

    public IReadOnlyList<ValidationIssue> Issues { get; }
}
