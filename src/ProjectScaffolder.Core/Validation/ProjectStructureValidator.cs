using System.Text.RegularExpressions;
using ProjectScaffolder.Core.Contracts;

namespace ProjectScaffolder.Core.Validation;

public sealed partial class ProjectStructureValidator
{
    public static ContractValidationResult Validate(ProjectStructureDocument structure)
    {
        ArgumentNullException.ThrowIfNull(structure);
        List<ValidationIssue> issues = [];

        if (structure.SchemaVersion != 1)
        {
            Add(issues, "schema.unsupported", "$.schemaVersion", "Only project structure schema version 1 is supported.");
        }

        if (!UuidRules.IsVersion7(structure.ProjectId))
        {
            Add(issues, "uuid.version", "$.projectId", "The project ID must be a UUIDv7 value.");
        }

        foreach ((string role, string relativePath) in structure.Paths)
        {
            string rolePath = $"$.paths['{role}']";
            if (!RolePattern().IsMatch(role))
            {
                Add(issues, "role.invalid", rolePath, "A semantic role must be a lowercase dotted identifier.");
            }

            ValidateRelativePath(relativePath, rolePath, issues);
        }

        return new ContractValidationResult(issues);
    }

    private static void ValidateRelativePath(string relativePath, string path, ICollection<ValidationIssue> issues)
    {
        string[] segments = relativePath.Split('/', StringSplitOptions.None);
        bool structurallyInvalid = string.IsNullOrWhiteSpace(relativePath) ||
            relativePath.Contains('\\') ||
            Path.IsPathRooted(relativePath) ||
            segments.Any(segment => string.IsNullOrEmpty(segment) || segment is "." or "..");

        if (structurallyInvalid || segments.Any(segment => WindowsNameValidator.GetError(segment) is not null))
        {
            Add(issues, "path.invalid", path, "A declared project path must be a safe normalized forward-slash path relative to the project root.");
        }
    }

    private static void Add(ICollection<ValidationIssue> issues, string code, string path, string message) =>
        issues.Add(new ValidationIssue(code, path, message));

    [GeneratedRegex("^[a-z][a-z0-9]*(?:\\.[a-z][a-z0-9]*)+$", RegexOptions.CultureInvariant)]
    private static partial Regex RolePattern();
}
