using ProjectScaffolder.Core.Contracts;
using ProjectScaffolder.Core.Paths;
using ProjectScaffolder.Core.Validation;
using ProjectScaffolder.Core.Variables;

namespace ProjectScaffolder.Core.Generation;

public static class GenerationPlanBuilder
{
    public static GenerationPlan Build(TemplateDocument template, GenerationContext context)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(context);

        List<ValidationIssue> issues = [.. TemplateValidator.Validate(template).Issues];
        if (issues.Count > 0)
        {
            throw new GenerationPlanException(issues);
        }

        IReadOnlyDictionary<string, string> variables = context.ToVariableValues();
        List<GenerationOperation> operations = [];
        Dictionary<string, (bool IsDirectory, string SourcePath)> outputs = new(StringComparer.OrdinalIgnoreCase);
        BuildNodes(template.Nodes, [], "$.nodes", template.DefaultFileCollision, context, variables, outputs, operations, issues);

        if (issues.Count > 0)
        {
            throw new GenerationPlanException(issues);
        }

        return new GenerationPlan(context, operations);
    }

    private static void BuildNodes(
        IReadOnlyList<TemplateNode> nodes,
        string[] parentComponents,
        string jsonPath,
        FileCollisionPolicy defaultCollision,
        GenerationContext context,
        IReadOnlyDictionary<string, string> variables,
        IDictionary<string, (bool IsDirectory, string SourcePath)> outputs,
        ICollection<GenerationOperation> operations,
        ICollection<ValidationIssue> issues)
    {
        for (int index = 0; index < nodes.Count; index++)
        {
            TemplateNode node = nodes[index];
            string nodePath = $"{jsonPath}[{index}]";
            string resolvedName;
            try
            {
                resolvedName = VariableResolver.Resolve(node.Name, variables);
            }
            catch (VariableResolutionException exception)
            {
                issues.Add(new ValidationIssue("variable.invalid", $"{nodePath}.name", exception.Message));
                continue;
            }

            string? nameError = WindowsNameValidator.GetError(resolvedName);
            if (nameError is not null)
            {
                issues.Add(new ValidationIssue("name.resolvedInvalid", $"{nodePath}.name", nameError));
                continue;
            }

            if (parentComponents.Length == 0 && resolvedName.Equals(".projectID.json", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new ValidationIssue("name.reserved", $"{nodePath}.name", "The resolved root name '.projectID.json' is reserved."));
                continue;
            }

            string[] components = [.. parentComponents, resolvedName];
            string relativePath = Path.Combine(components);
            string fullPath;
            try
            {
                fullPath = PathSafety.ResolveDescendant(context.ProjectPath, components);
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or NotSupportedException)
            {
                issues.Add(new ValidationIssue("path.unsafe", $"{nodePath}.name", exception.Message));
                continue;
            }

            bool isDirectory = node is FolderNode;
            if (outputs.TryGetValue(fullPath, out (bool IsDirectory, string SourcePath) previous))
            {
                string conflict = previous.IsDirectory == isDirectory ? "duplicate output" : "file/folder conflict";
                issues.Add(new ValidationIssue("path.duplicate", $"{nodePath}.name", $"The resolved path conflicts with {previous.SourcePath} ({conflict})."));
                continue;
            }

            outputs.Add(fullPath, (isDirectory, nodePath));
            switch (node)
            {
                case FolderNode folder:
                    operations.Add(new CreateDirectoryOperation(folder.NodeId, relativePath, fullPath));
                    BuildNodes(folder.Children, components, $"{nodePath}.children", defaultCollision, context, variables, outputs, operations, issues);
                    break;
                case TextFileNode textFile:
                    try
                    {
                        string content = VariableResolver.Resolve(textFile.Content, variables);
                        operations.Add(new CreateTextFileOperation(
                            textFile.NodeId,
                            relativePath,
                            fullPath,
                            textFile.Collision ?? defaultCollision,
                            content,
                            textFile.Encoding,
                            textFile.LineEndings));
                    }
                    catch (VariableResolutionException exception)
                    {
                        issues.Add(new ValidationIssue("variable.invalid", $"{nodePath}.content", exception.Message));
                    }

                    break;
                case AssetFileNode assetFile:
                    operations.Add(new CopyAssetFileOperation(
                        assetFile.NodeId,
                        relativePath,
                        fullPath,
                        assetFile.Collision ?? defaultCollision,
                        assetFile.Asset.Path,
                        assetFile.Asset.Sha256));
                    break;
            }
        }
    }
}
