namespace ProjectScaffolder.Core.Validation;

public sealed record ValidationIssue(string Code, string Path, string Message);

public sealed class ContractValidationResult
{
    public ContractValidationResult(IEnumerable<ValidationIssue> issues)
    {
        Issues = issues.ToArray();
    }

    public IReadOnlyList<ValidationIssue> Issues { get; }

    public bool IsValid => Issues.Count == 0;
}
