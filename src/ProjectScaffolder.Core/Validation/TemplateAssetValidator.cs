using System.Security.Cryptography;
using ProjectScaffolder.Core.Contracts;
using ProjectScaffolder.Core.Paths;

namespace ProjectScaffolder.Core.Validation;

public static class TemplateAssetValidator
{
    public static ContractValidationResult Validate(TemplateDocument template, string templateDirectory)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentException.ThrowIfNullOrWhiteSpace(templateDirectory);

        List<ValidationIssue> issues = [];
        ValidateNodes(template.Nodes, "$.nodes", Path.GetFullPath(templateDirectory), issues);
        return new ContractValidationResult(issues);
    }

    private static void ValidateNodes(
        IReadOnlyList<TemplateNode> nodes,
        string path,
        string templateDirectory,
        ICollection<ValidationIssue> issues)
    {
        for (int index = 0; index < nodes.Count; index++)
        {
            TemplateNode node = nodes[index];
            string nodePath = $"{path}[{index}]";
            if (node is FolderNode folder)
            {
                ValidateNodes(folder.Children, $"{nodePath}.children", templateDirectory, issues);
            }
            else if (node is AssetFileNode asset)
            {
                ValidateAsset(asset, nodePath, templateDirectory, issues);
            }
        }
    }

    private static void ValidateAsset(
        AssetFileNode asset,
        string path,
        string templateDirectory,
        ICollection<ValidationIssue> issues)
    {
        string[] components = asset.Asset.Path.Split('/');
        if (components.Length < 2 || !components[0].Equals("assets", StringComparison.Ordinal))
        {
            issues.Add(new ValidationIssue("asset.path", $"{path}.asset.path", "The asset path must begin with assets/."));
            return;
        }

        string fullPath;
        try
        {
            fullPath = PathSafety.ResolveDescendant(templateDirectory, components);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or NotSupportedException)
        {
            issues.Add(new ValidationIssue("asset.path", $"{path}.asset.path", exception.Message));
            return;
        }

        if (HasReparsePoint(templateDirectory, components))
        {
            issues.Add(new ValidationIssue("asset.reparsePoint", $"{path}.asset.path", "The asset path contains a symbolic link or reparse point."));
            return;
        }

        if (!File.Exists(fullPath))
        {
            issues.Add(new ValidationIssue("asset.missing", $"{path}.asset.path", "The attached asset file does not exist."));
            return;
        }

        using FileStream stream = File.Open(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        string actualDigest = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        if (!actualDigest.Equals(asset.Asset.Sha256, StringComparison.Ordinal))
        {
            issues.Add(new ValidationIssue("asset.hash", $"{path}.asset.sha256", "The attached asset does not match its recorded SHA-256 digest."));
        }
    }

    private static bool HasReparsePoint(string root, IReadOnlyList<string> components)
    {
        string current = Path.GetFullPath(root);
        foreach (string component in components)
        {
            current = Path.Combine(current, component);
            if ((File.Exists(current) || Directory.Exists(current)) &&
                File.GetAttributes(current).HasFlag(FileAttributes.ReparsePoint))
            {
                return true;
            }
        }

        return false;
    }
}
