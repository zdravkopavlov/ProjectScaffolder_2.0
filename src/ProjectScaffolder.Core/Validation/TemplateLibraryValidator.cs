using ProjectScaffolder.Core.Contracts;

namespace ProjectScaffolder.Core.Validation;

public sealed class TemplateLibraryValidator
{
    public static ContractValidationResult Validate(TemplateLibraryDocument library)
    {
        ArgumentNullException.ThrowIfNull(library);
        List<ValidationIssue> issues = [];

        if (library.SchemaVersion != 1)
        {
            issues.Add(new ValidationIssue("schema.unsupported", "$.schemaVersion", "Only template library schema version 1 is supported."));
        }

        ValidateIds(library.TemplateOrder, "$.templateOrder", issues);
        ValidateIds(library.ExplorerTemplateIds, "$.explorerTemplateIds", issues);
        return new ContractValidationResult(issues);
    }

    private static void ValidateIds(IReadOnlyList<Guid> ids, string path, List<ValidationIssue> issues)
    {
        HashSet<Guid> uniqueIds = [];
        for (int index = 0; index < ids.Count; index++)
        {
            if (!UuidRules.IsVersion7(ids[index]))
            {
                issues.Add(new ValidationIssue("uuid.version", $"{path}[{index}]", "Template IDs must be UUIDv7 values."));
            }

            if (!uniqueIds.Add(ids[index]))
            {
                issues.Add(new ValidationIssue("uuid.duplicate", $"{path}[{index}]", "Template IDs cannot be duplicated."));
            }
        }
    }
}
