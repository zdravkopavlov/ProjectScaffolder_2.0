namespace ProjectScaffolder.Core.Paths;

public static class PathSafety
{
    public static string ResolveDescendant(string rootPath, IEnumerable<string> components)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentNullException.ThrowIfNull(components);

        string canonicalRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
        string candidate = canonicalRoot;
        foreach (string component in components)
        {
            if (string.IsNullOrEmpty(component) ||
                component is "." or ".." ||
                Path.IsPathRooted(component) ||
                component.Contains(Path.DirectorySeparatorChar) ||
                component.Contains(Path.AltDirectorySeparatorChar))
            {
                throw new ArgumentException($"'{component}' is not a single relative path component.", nameof(components));
            }

            candidate = Path.Combine(candidate, component);
        }

        string fullPath = Path.GetFullPath(candidate);
        if (!IsStrictDescendant(canonicalRoot, fullPath))
        {
            throw new InvalidOperationException($"Resolved path '{fullPath}' escapes the target root '{canonicalRoot}'.");
        }

        return fullPath;
    }

    public static bool IsStrictDescendant(string rootPath, string candidatePath)
    {
        string canonicalRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
        string canonicalCandidate = Path.GetFullPath(candidatePath);
        string rootWithSeparator = canonicalRoot + Path.DirectorySeparatorChar;
        return canonicalCandidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
    }
}
