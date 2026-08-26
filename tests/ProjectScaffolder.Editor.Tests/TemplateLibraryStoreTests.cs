using System.IO;
using ProjectScaffolder.Core.Contracts;
using ProjectScaffolder.Core.Serialization;
using ProjectScaffolder.Editor.Services;

namespace ProjectScaffolder.Editor.Tests;

public sealed class TemplateLibraryStoreTests
{
    [Fact]
    public async Task SavesLoadsAndPersistsExplorerIntent()
    {
        using TestDataRoot root = new();
        TemplateLibraryStore store = new(new EditorPaths(root.Path));
        TemplateDocument template = CreateTemplate();

        await store.LoadAsync();
        await store.SaveTemplateAsync(template);
        await store.SetExplorerEnabledAsync(template.TemplateId, true);

        LoadedTemplateLibrary reloaded = await new TemplateLibraryStore(new EditorPaths(root.Path)).LoadAsync();
        StoredTemplate item = Assert.Single(reloaded.Templates, entry => entry.TemplateId == template.TemplateId);
        Assert.Equal(template.Name, item.Document?.Name);
        Assert.Contains(template.TemplateId, reloaded.ExplorerTemplateIds);
    }

    [Fact]
    public async Task KeepsDamagedTemplateVisible()
    {
        using TestDataRoot root = new();
        Guid id = Guid.CreateVersion7();
        string directory = System.IO.Path.Combine(root.Path, "Templates", id.ToString("D"));
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(System.IO.Path.Combine(directory, "template.json"), "{ not valid json }");

        LoadedTemplateLibrary loaded = await new TemplateLibraryStore(new EditorPaths(root.Path)).LoadAsync();

        StoredTemplate damaged = Assert.Single(loaded.Templates, entry => entry.TemplateId == id);
        Assert.Null(damaged.Document);
        Assert.NotNull(damaged.ErrorMessage);
    }

    [Fact]
    public async Task RemovalMovesPackageToRecoverableTrashAndUpdatesLibrary()
    {
        using TestDataRoot root = new();
        TemplateLibraryStore store = new(new EditorPaths(root.Path));
        TemplateDocument template = CreateTemplate();
        await store.LoadAsync();
        await store.SaveTemplateAsync(template);
        await store.SetExplorerEnabledAsync(template.TemplateId, true);

        string? trashPath = await store.RemoveTemplateAsync(template.TemplateId);

        Assert.NotNull(trashPath);
        Assert.True(Directory.Exists(trashPath));
        Assert.True(File.Exists(System.IO.Path.Combine(trashPath, "template.json")));
        Assert.False(Directory.Exists(System.IO.Path.Combine(root.Path, "Templates", template.TemplateId.ToString("D"))));
        LoadedTemplateLibrary reloaded = await new TemplateLibraryStore(new EditorPaths(root.Path)).LoadAsync();
        Assert.DoesNotContain(reloaded.Templates, item => item.TemplateId == template.TemplateId);
        Assert.DoesNotContain(template.TemplateId, reloaded.ExplorerTemplateIds);
    }

    [Fact]
    public async Task StagesAndCommitsAttachedFileIntoTemplatePackage()
    {
        using TestDataRoot root = new();
        TemplateLibraryStore store = new(new EditorPaths(root.Path));
        await store.LoadAsync();
        byte[] content = [0, 1, 2, 3, 254, 255];
        string sourcePath = System.IO.Path.Combine(root.Path, "Reference ü.bin");
        await File.WriteAllBytesAsync(sourcePath, content);
        Guid nodeId = Guid.CreateVersion7();
        TemplateDocument template = CreateTemplate();

        StagedAsset staged = await store.StageAssetAsync(template.TemplateId, nodeId, sourcePath);
        template = WithAsset(template, nodeId, staged);
        await store.SaveTemplateAsync(template);

        string packageAsset = System.IO.Path.Combine(
            root.Path,
            "Templates",
            template.TemplateId.ToString("D"),
            staged.RelativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));
        Assert.Equal(content, await File.ReadAllBytesAsync(packageAsset));
        Assert.DoesNotContain('ü', staged.RelativePath);
        Assert.False(Directory.Exists(System.IO.Path.Combine(root.Path, "Staging", template.TemplateId.ToString("D"))));
    }

    [Fact]
    public async Task DuplicateStagesIndependentCopiesOfAttachedAssets()
    {
        using TestDataRoot root = new();
        TemplateLibraryStore store = new(new EditorPaths(root.Path));
        await store.LoadAsync();
        byte[] content = [10, 20, 30, 40];
        string sourcePath = System.IO.Path.Combine(root.Path, "starter.max");
        await File.WriteAllBytesAsync(sourcePath, content);
        Guid nodeId = Guid.CreateVersion7();
        TemplateDocument source = CreateTemplate();
        StagedAsset staged = await store.StageAssetAsync(source.TemplateId, nodeId, sourcePath);
        source = WithAsset(source, nodeId, staged);
        await store.SaveTemplateAsync(source);
        TemplateDocument duplicate = source with
        {
            TemplateId = Guid.CreateVersion7(),
            Name = "Stored Template Copy",
            Nodes = source.Nodes.Select(node => node with { NodeId = Guid.CreateVersion7() }).ToArray(),
        };

        await store.StageAssetsForDuplicateAsync(source.TemplateId, duplicate);
        await store.SaveTemplateAsync(duplicate);

        AssetFileNode duplicateNode = Assert.IsType<AssetFileNode>(Assert.Single(duplicate.Nodes));
        string duplicateAsset = System.IO.Path.Combine(
            root.Path,
            "Templates",
            duplicate.TemplateId.ToString("D"),
            duplicateNode.Asset.Path.Replace('/', System.IO.Path.DirectorySeparatorChar));
        Assert.Equal(content, await File.ReadAllBytesAsync(duplicateAsset));
    }

    private static TemplateDocument CreateTemplate()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        return new TemplateDocument
        {
            SchemaVersion = 1,
            TemplateId = Guid.CreateVersion7(),
            Name = "Stored Template",
            TemplateVersion = 1,
            CreatedAtUtc = now,
            ModifiedAtUtc = now,
            DefaultFileCollision = FileCollisionPolicy.Skip,
            Nodes = [],
        };
    }

    private static TemplateDocument WithAsset(TemplateDocument template, Guid nodeId, StagedAsset staged) => template with
    {
        Nodes =
        [
            new AssetFileNode
            {
                NodeId = nodeId,
                Name = staged.OriginalFileName,
                Asset = new AssetReference
                {
                    Path = staged.RelativePath,
                    Sha256 = staged.Sha256,
                },
            },
        ],
    };

    private sealed class TestDataRoot : IDisposable
    {
        public TestDataRoot()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ProjectScaffolder.EditorTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, true);
    }
}
