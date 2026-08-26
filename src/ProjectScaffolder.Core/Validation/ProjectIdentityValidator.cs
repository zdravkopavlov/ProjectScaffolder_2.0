using ProjectScaffolder.Core.Contracts;

namespace ProjectScaffolder.Core.Validation;

public sealed class ProjectIdentityValidator
{
    public static ContractValidationResult Validate(ProjectIdentityDocument identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        List<ValidationIssue> issues = [];

        if (identity.SchemaVersion != 1)
        {
            issues.Add(new ValidationIssue("schema.unsupported", "$.schemaVersion", "Only project identity schema version 1 is supported."));
        }

        if (!UuidRules.IsVersion7(identity.ProjectId))
        {
            issues.Add(new ValidationIssue("uuid.version", "$.projectId", "The project ID must be a UUIDv7 value."));
        }

        if (!UuidRules.IsVersion7(identity.Template.Id))
        {
            issues.Add(new ValidationIssue("uuid.version", "$.template.id", "The template ID must be a UUIDv7 value."));
        }

        if (identity.CreatedAtUtc.Offset != TimeSpan.Zero)
        {
            issues.Add(new ValidationIssue("timestamp.utc", "$.createdAtUtc", "The creation timestamp must use UTC."));
        }

        if (identity.ProjectName.Length is < 1 or > 255)
        {
            issues.Add(new ValidationIssue("string.length", "$.projectName", "The project name must contain between 1 and 255 characters."));
        }

        if (identity.Template.Name.Length is < 1 or > 120)
        {
            issues.Add(new ValidationIssue("string.length", "$.template.name", "The template name must contain between 1 and 120 characters."));
        }

        if (identity.Template.Version < 1)
        {
            issues.Add(new ValidationIssue("version.invalid", "$.template.version", "The template version must be at least 1."));
        }

        if (!identity.Generator.Name.Equals("Project Scaffolder", StringComparison.Ordinal))
        {
            issues.Add(new ValidationIssue("generator.name", "$.generator.name", "The generator name must be exactly 'Project Scaffolder'."));
        }

        if (identity.Generator.Version.Length is < 1 or > 64)
        {
            issues.Add(new ValidationIssue("string.length", "$.generator.version", "The generator version must contain between 1 and 64 characters."));
        }

        return new ContractValidationResult(issues);
    }
}
