using ProjectScaffolder.Core.Contracts;
using ProjectScaffolder.Editor.ViewModels;

namespace ProjectScaffolder.Editor.Tests;

public sealed class TemplateEditorViewModelTests
{
    [Fact]
    public void EditingAndStructureChangesSetDirtyStateAndValidation()
    {
        TemplateEditorViewModel editor = new(CreateTemplate());
        Assert.False(editor.IsDirty);
        Assert.True(editor.IsValid);

        EditorNodeViewModel folder = EditorNodeViewModel.CreateFolder("SCENES");
        editor.AddRoot(folder);
        editor.NotifyStructureChanged();

        Assert.True(editor.IsDirty);
        Assert.Equal(1, editor.FolderCount);
        editor.Name = string.Empty;
        Assert.False(editor.IsValid);
        Assert.Contains("between 1 and 120", editor.ValidationSummary, StringComparison.Ordinal);
    }

    [Fact]
    public void DuplicateCreatesFreshTemplateAndNodeIds()
    {
        TemplateDocument sourceDocument = CreateTemplate() with
        {
            Nodes =
            [
                new FolderNode
                {
                    NodeId = Guid.CreateVersion7(),
                    Name = "ASSETS",
                    Children =
                    [
                        new TextFileNode
                        {
                            NodeId = Guid.CreateVersion7(),
                            Name = "notes.txt",
                            Encoding = TextFileEncodingKind.Utf8,
                            LineEndings = LineEndingKind.CrLf,
                            Content = string.Empty,
                        },
                    ],
                },
            ],
        };
        TemplateEditorViewModel source = new(sourceDocument);

        TemplateEditorViewModel duplicate = TemplateEditorViewModel.Duplicate(source);

        Assert.NotEqual(source.TemplateId, duplicate.TemplateId);
        Assert.NotEqual(source.Nodes[0].NodeId, duplicate.Nodes[0].NodeId);
        Assert.NotEqual(source.Nodes[0].Children[0].NodeId, duplicate.Nodes[0].Children[0].NodeId);
        Assert.True(duplicate.IsNew);
        Assert.True(duplicate.IsDirty);
    }

    [Fact]
    public void ResolvedPathCollisionIsContinuouslyInvalid()
    {
        TemplateEditorViewModel editor = new(CreateTemplate());
        editor.AddRoot(EditorNodeViewModel.CreateFolder("SCENES"));
        editor.AddRoot(EditorNodeViewModel.CreateFolder("scenes"));
        editor.NotifyStructureChanged();

        Assert.False(editor.IsValid);
        Assert.Contains("resolved path conflicts", editor.ValidationSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FileCollisionOverrideCanReturnToTemplateDefault()
    {
        EditorNodeViewModel file = EditorNodeViewModel.CreateTextFile("notes.txt");
        file.Collision = FileCollisionPolicy.Skip;

        file.Collision = null;

        Assert.Null(file.Collision);
    }

    [Fact]
    public void AttachedFileReplacementUpdatesPackageReferenceAndDirtyState()
    {
        EditorNodeViewModel asset = EditorNodeViewModel.CreateAssetFile(
            Guid.CreateVersion7(),
            "starter.max",
            "assets/old/starter.max",
            new string('a', 64));
        TemplateEditorViewModel editor = new(CreateTemplate() with
        {
            Nodes = [asset.ToContract()],
        });
        EditorNodeViewModel loadedAsset = editor.Nodes[0];

        loadedAsset.ReplaceAsset("assets/new/replacement.max", new string('b', 64));

        Assert.True(editor.IsDirty);
        Assert.Equal("replacement.max", loadedAsset.AssetFileName);
        Assert.Contains(new string('b', 64), loadedAsset.AssetDigestDisplay, StringComparison.Ordinal);
    }

    private static TemplateDocument CreateTemplate()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        return new TemplateDocument
        {
            SchemaVersion = 1,
            TemplateId = Guid.CreateVersion7(),
            Name = "Test Template",
            TemplateVersion = 1,
            CreatedAtUtc = now,
            ModifiedAtUtc = now,
            DefaultFileCollision = FileCollisionPolicy.Skip,
            Nodes = [],
        };
    }
}
