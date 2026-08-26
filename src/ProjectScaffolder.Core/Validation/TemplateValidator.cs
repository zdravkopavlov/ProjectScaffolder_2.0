using System.Text.RegularExpressions;
using ProjectScaffolder.Core.Contracts;
using ProjectScaffolder.Core.Variables;

namespace ProjectScaffolder.Core.Validation;

public sealed partial class TemplateValidator
{
    public static ContractValidationResult Validate(TemplateDocument template)
    {
        ArgumentNullException.ThrowIfNull(template);
        List<ValidationIssue> issues = [];

        if (template.SchemaVersion != 1)
        {
            Add(issues, "schema.unsupported", "$.schemaVersion", "Only template schema version 1 is supported.");
        }

        ValidateUuid(template.TemplateId, "$.templateId", "template", issues);
        ValidateLength(template.Name, 1, 120, "$.name", issues);
        if (template.Description is { Length: > 2000 })
        {
            Add(issues, "string.tooLong", "$.description", "The description cannot exceed 2000 characters.");
        }

        if (template.TemplateVersion < 1)
        {
            Add(issues, "version.invalid", "$.templateVersion", "The template version must be at least 1.");
        }

        ValidateUtc(template.CreatedAtUtc, "$.createdAtUtc", issues);
        ValidateUtc(template.ModifiedAtUtc, "$.modifiedAtUtc", issues);
        if (template.ModifiedAtUtc < template.CreatedAtUtc)
        {
            Add(issues, "timestamp.order", "$.modifiedAtUtc", "The modified timestamp cannot precede the created timestamp.");
        }

        HashSet<Guid> nodeIds = [];
        ValidateNodes(template.Nodes, "$.nodes", true, nodeIds, issues);
        return new ContractValidationResult(issues);
    }

    private static void ValidateNodes(
        IReadOnlyList<TemplateNode> nodes,
        string path,
        bool atRoot,
        ISet<Guid> nodeIds,
        ICollection<ValidationIssue> issues)
    {
        for (int index = 0; index < nodes.Count; index++)
        {
            TemplateNode node = nodes[index];
            string nodePath = $"{path}[{index}]";
            ValidateUuid(node.NodeId, $"{nodePath}.nodeId", "node", issues);
            if (!nodeIds.Add(node.NodeId))
            {
                Add(issues, "node.duplicateId", $"{nodePath}.nodeId", "Node IDs must be unique across the complete template.");
            }

            ValidateNodeName(node.Name, $"{nodePath}.name", atRoot, issues);

            switch (node)
            {
                case FolderNode folder:
                    ValidateNodes(folder.Children, $"{nodePath}.children", false, nodeIds, issues);
                    break;
                case TextFileNode textFile:
                    ValidateVariables(textFile.Content, $"{nodePath}.content", issues);
                    break;
                case AssetFileNode assetFile:
                    ValidateAsset(assetFile.Asset, $"{nodePath}.asset", issues);
                    break;
                default:
                    Add(issues, "node.unsupported", nodePath, $"Unsupported node type '{node.GetType().Name}'.");
                    break;
            }
        }
    }

    private static void ValidateNodeName(string name, string path, bool atRoot, ICollection<ValidationIssue> issues)
    {
        string? nameError = WindowsNameValidator.GetError(name);
        if (nameError is not null)
        {
            Add(issues, "name.invalid", path, nameError);
        }

        ValidateVariables(name, path, issues);
        if (atRoot && name.Equals(".projectID.json", StringComparison.OrdinalIgnoreCase))
        {
            Add(issues, "name.reserved", path, "The root name '.projectID.json' is reserved for project identity metadata.");
        }
    }

    private static void ValidateAsset(AssetReference asset, string path, ICollection<ValidationIssue> issues)
    {
        string[] segments = asset.Path.Split('/', StringSplitOptions.None);
        bool invalidPath = !asset.Path.StartsWith("assets/", StringComparison.Ordinal) ||
            asset.Path.Contains('\\') ||
            Path.IsPathRooted(asset.Path) ||
            segments.Any(segment => string.IsNullOrEmpty(segment) || segment is "." or "..");

        if (invalidPath || !AssetPathPattern().IsMatch(asset.Path))
        {
            Add(issues, "asset.path", $"{path}.path", "The asset path must be a safe forward-slash path beneath assets/.");
        }

        if (!Sha256Pattern().IsMatch(asset.Sha256))
        {
            Add(issues, "asset.sha256", $"{path}.sha256", "The asset digest must contain exactly 64 lowercase hexadecimal characters.");
        }
    }

    private static void ValidateVariables(string value, string path, ICollection<ValidationIssue> issues)
    {
        string? error = VariableResolver.GetSyntaxError(value);
        if (error is not null)
        {
            Add(issues, "variable.invalid", path, error);
        }
    }

    private static void ValidateLength(string value, int minimum, int maximum, string path, ICollection<ValidationIssue> issues)
    {
        if (value.Length < minimum || value.Length > maximum)
        {
            Add(issues, "string.length", path, $"The value must contain between {minimum} and {maximum} characters.");
        }
    }

    private static void ValidateUuid(Guid value, string path, string label, ICollection<ValidationIssue> issues)
    {
        if (!UuidRules.IsVersion7(value))
        {
            Add(issues, "uuid.version", path, $"The {label} ID must be a UUIDv7 value.");
        }
    }

    private static void ValidateUtc(DateTimeOffset value, string path, ICollection<ValidationIssue> issues)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            Add(issues, "timestamp.utc", path, "The timestamp must use UTC.");
        }
    }

    private static void Add(ICollection<ValidationIssue> issues, string code, string path, string message) =>
        issues.Add(new ValidationIssue(code, path, message));

    [GeneratedRegex("^assets/[A-Za-z0-9._/-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex AssetPathPattern();

    [GeneratedRegex("^[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256Pattern();
}
