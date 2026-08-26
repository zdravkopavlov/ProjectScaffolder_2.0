using ProjectScaffolder.Core.Contracts;
using ProjectScaffolder.Core.Validation;

namespace ProjectScaffolder.Core.Generation;

public enum PreflightDisposition
{
    Create,
    ReuseDirectory,
    SkipExistingFile,
    OverwriteExistingFile,
}

public sealed record PreflightOperation(GenerationOperation Operation, PreflightDisposition Disposition);

public sealed record GenerationPreflightResult(
    IReadOnlyList<PreflightOperation> Operations,
    IReadOnlyList<ValidationIssue> Issues)
{
    public bool IsValid => Issues.Count == 0;
}

public static class GenerationPreflight
{
    public static GenerationPreflightResult Inspect(GenerationPlan plan, IFileSystemProbe fileSystem)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(fileSystem);

        List<PreflightOperation> operations = [];
        List<ValidationIssue> issues = [];

        foreach (GenerationOperation operation in plan.Operations)
        {
            FileSystemEntryKind entryKind = fileSystem.GetEntryKind(operation.FullPath);
            switch (operation)
            {
                case CreateDirectoryOperation:
                    InspectDirectory(operation, entryKind, operations, issues);
                    break;
                case CreateFileOperation file:
                    InspectFile(file, entryKind, operations, issues);
                    break;
            }
        }

        return new GenerationPreflightResult(operations, issues);
    }

    private static void InspectDirectory(
        GenerationOperation operation,
        FileSystemEntryKind entryKind,
        List<PreflightOperation> operations,
        List<ValidationIssue> issues)
    {
        switch (entryKind)
        {
            case FileSystemEntryKind.None:
                operations.Add(new PreflightOperation(operation, PreflightDisposition.Create));
                break;
            case FileSystemEntryKind.Directory:
                operations.Add(new PreflightOperation(operation, PreflightDisposition.ReuseDirectory));
                break;
            case FileSystemEntryKind.File:
                issues.Add(Conflict(operation, "A file already occupies the required directory path."));
                break;
            case FileSystemEntryKind.ReparsePoint:
                issues.Add(Conflict(operation, "Generation will not traverse an existing reparse point."));
                break;
        }
    }

    private static void InspectFile(
        CreateFileOperation operation,
        FileSystemEntryKind entryKind,
        List<PreflightOperation> operations,
        List<ValidationIssue> issues)
    {
        switch (entryKind)
        {
            case FileSystemEntryKind.None:
                operations.Add(new PreflightOperation(operation, PreflightDisposition.Create));
                break;
            case FileSystemEntryKind.Directory:
                issues.Add(Conflict(operation, "A directory already occupies the required file path."));
                break;
            case FileSystemEntryKind.ReparsePoint:
                issues.Add(Conflict(operation, "Generation will not replace or traverse an existing reparse point."));
                break;
            case FileSystemEntryKind.File:
                switch (operation.Collision)
                {
                    case FileCollisionPolicy.Skip:
                        operations.Add(new PreflightOperation(operation, PreflightDisposition.SkipExistingFile));
                        break;
                    case FileCollisionPolicy.Overwrite:
                        operations.Add(new PreflightOperation(operation, PreflightDisposition.OverwriteExistingFile));
                        break;
                    case FileCollisionPolicy.Fail:
                        issues.Add(Conflict(operation, "The file already exists and its collision policy is 'fail'."));
                        break;
                }

                break;
        }
    }

    private static ValidationIssue Conflict(GenerationOperation operation, string message) =>
        new("target.conflict", operation.RelativePath, message);
}
