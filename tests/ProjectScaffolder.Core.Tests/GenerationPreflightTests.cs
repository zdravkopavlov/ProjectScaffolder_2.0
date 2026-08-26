using ProjectScaffolder.Core.Contracts;
using ProjectScaffolder.Core.Generation;
using ProjectScaffolder.Core.Variables;

namespace ProjectScaffolder.Core.Tests;

public sealed class GenerationPreflightTests
{
    [Fact]
    public void AppliesCollisionPoliciesWithoutWriting()
    {
        GenerationContext context = GenerationContext.CreateNew(Path.Combine(Path.GetTempPath(), "Preflight Test"));
        GenerationPlan plan = new(context, [
            new CreateDirectoryOperation(Guid.NewGuid(), "SCENES", Path.Combine(context.ProjectPath, "SCENES")),
            CreateFile(context, "keep.txt", FileCollisionPolicy.Skip),
            CreateFile(context, "replace.txt", FileCollisionPolicy.Overwrite),
            CreateFile(context, "blocked.txt", FileCollisionPolicy.Fail),
        ]);
        FakeFileSystemProbe probe = new(new Dictionary<string, FileSystemEntryKind>(StringComparer.OrdinalIgnoreCase)
        {
            [Path.Combine(context.ProjectPath, "SCENES")] = FileSystemEntryKind.Directory,
            [Path.Combine(context.ProjectPath, "keep.txt")] = FileSystemEntryKind.File,
            [Path.Combine(context.ProjectPath, "replace.txt")] = FileSystemEntryKind.File,
            [Path.Combine(context.ProjectPath, "blocked.txt")] = FileSystemEntryKind.File,
        });

        GenerationPreflightResult result = GenerationPreflight.Inspect(plan, probe);

        Assert.False(result.IsValid);
        Assert.Collection(
            result.Operations,
            operation => Assert.Equal(PreflightDisposition.ReuseDirectory, operation.Disposition),
            operation => Assert.Equal(PreflightDisposition.SkipExistingFile, operation.Disposition),
            operation => Assert.Equal(PreflightDisposition.OverwriteExistingFile, operation.Disposition));
        Assert.Contains(result.Issues, issue => issue.Path == "blocked.txt");
    }

    [Fact]
    public void RejectsReparsePoints()
    {
        GenerationContext context = GenerationContext.CreateNew(Path.Combine(Path.GetTempPath(), "Reparse Test"));
        CreateFileOperation file = CreateFile(context, "linked.txt", FileCollisionPolicy.Overwrite);
        GenerationPlan plan = new(context, [file]);
        FakeFileSystemProbe probe = new(new Dictionary<string, FileSystemEntryKind>
        {
            [file.FullPath] = FileSystemEntryKind.ReparsePoint,
        });

        GenerationPreflightResult result = GenerationPreflight.Inspect(plan, probe);

        Assert.False(result.IsValid);
        Assert.Single(result.Issues);
        Assert.Empty(result.Operations);
    }

    private static CreateTextFileOperation CreateFile(
        GenerationContext context,
        string name,
        FileCollisionPolicy collision) => new(
            Guid.NewGuid(),
            name,
            Path.Combine(context.ProjectPath, name),
            collision,
            string.Empty,
            TextFileEncodingKind.Utf8,
            LineEndingKind.CrLf);

    private sealed class FakeFileSystemProbe(IReadOnlyDictionary<string, FileSystemEntryKind> entries) : IFileSystemProbe
    {
        public FileSystemEntryKind GetEntryKind(string path) =>
            entries.TryGetValue(path, out FileSystemEntryKind kind) ? kind : FileSystemEntryKind.None;
    }
}
