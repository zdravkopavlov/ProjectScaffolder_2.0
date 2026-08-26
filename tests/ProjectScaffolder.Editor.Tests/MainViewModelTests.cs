using System.IO;
using ProjectScaffolder.Editor.Services;
using ProjectScaffolder.Editor.ViewModels;

namespace ProjectScaffolder.Editor.Tests;

public sealed class MainViewModelTests
{
    [Fact]
    public void ProjectRootSelectionCreatesFirstLevelItems()
    {
        MainViewModel viewModel = CreateViewModel();
        viewModel.NewTemplateCommand.Execute(null);

        viewModel.SelectedNode = null;
        viewModel.AddFolderCommand.Execute(null);
        EditorNodeViewModel firstFolder = Assert.Single(viewModel.SelectedTemplate!.Nodes);

        viewModel.SelectedNode = null;
        viewModel.AddTextFileCommand.Execute(null);

        Assert.Equal(2, viewModel.SelectedTemplate.Nodes.Count);
        Assert.Empty(firstFolder.Children);
        Assert.True(viewModel.SelectedTemplate.Nodes[1].IsTextFile);
    }

    [Fact]
    public void FileSelectionCreatesSiblingInTheSameFolder()
    {
        MainViewModel viewModel = CreateViewModel();
        viewModel.NewTemplateCommand.Execute(null);
        viewModel.AddFolderCommand.Execute(null);
        EditorNodeViewModel folder = viewModel.SelectedTemplate!.Nodes[0];
        viewModel.SelectedNode = folder;
        viewModel.AddTextFileCommand.Execute(null);
        EditorNodeViewModel firstFile = folder.Children[0];

        viewModel.SelectedNode = firstFile;
        viewModel.AddTextFileCommand.Execute(null);

        Assert.Equal(2, folder.Children.Count);
        Assert.All(folder.Children, node => Assert.True(node.IsTextFile));
    }

    [Fact]
    public void MoveNodeSupportsNestingAndReturningToRoot()
    {
        MainViewModel viewModel = CreateViewModel();
        viewModel.NewTemplateCommand.Execute(null);
        viewModel.AddFolderCommand.Execute(null);
        EditorNodeViewModel first = viewModel.SelectedTemplate!.Nodes[0];
        viewModel.AddFolderCommand.Execute(null);
        EditorNodeViewModel second = viewModel.SelectedTemplate.Nodes[1];

        Assert.True(viewModel.MoveNode(first, second, 0));
        Assert.Same(first, Assert.Single(second.Children));
        Assert.True(viewModel.MoveNode(first, null, viewModel.SelectedTemplate.Nodes.Count));
        Assert.Contains(first, viewModel.SelectedTemplate.Nodes);
    }

    [Fact]
    public void MoveNodeRejectsDroppingFolderInsideItsDescendant()
    {
        MainViewModel viewModel = CreateViewModel();
        viewModel.NewTemplateCommand.Execute(null);
        viewModel.AddFolderCommand.Execute(null);
        EditorNodeViewModel parent = viewModel.SelectedTemplate!.Nodes[0];
        viewModel.SelectedNode = parent;
        viewModel.AddFolderCommand.Execute(null);
        EditorNodeViewModel child = parent.Children[0];

        Assert.False(viewModel.CanMoveNode(parent, child));
        Assert.False(viewModel.MoveNode(parent, child, 0));
        Assert.Same(parent, Assert.Single(viewModel.SelectedTemplate!.Nodes));
    }

    [Fact]
    public void RenameCommandBeginsInlineEditingForSelectedNode()
    {
        MainViewModel viewModel = CreateViewModel();
        viewModel.NewTemplateCommand.Execute(null);
        viewModel.AddFolderCommand.Execute(null);
        viewModel.SelectedNode = viewModel.SelectedTemplate!.Nodes[0];

        viewModel.RenameNodeCommand.Execute(null);

        Assert.True(viewModel.SelectedNode!.IsEditing);
    }

    [Fact]
    public void RepeatedAddsKeepTheOriginalFolderSelected()
    {
        MainViewModel viewModel = CreateViewModel();
        viewModel.NewTemplateCommand.Execute(null);
        viewModel.AddFolderCommand.Execute(null);
        EditorNodeViewModel parent = viewModel.SelectedTemplate!.Nodes[0];
        viewModel.SelectedNode = parent;

        viewModel.AddFolderCommand.Execute(null);
        viewModel.AddFolderCommand.Execute(null);
        viewModel.AddFolderCommand.Execute(null);

        Assert.Same(parent, viewModel.SelectedNode);
        Assert.Equal(3, parent.Children.Count);
        Assert.All(parent.Children, child => Assert.Empty(child.Children));
    }

    [Fact]
    public void SortOrdersEveryLevelWithFoldersBeforeFiles()
    {
        MainViewModel viewModel = CreateViewModel();
        viewModel.NewTemplateCommand.Execute(null);
        TemplateEditorViewModel template = viewModel.SelectedTemplate!;
        EditorNodeViewModel zFolder = EditorNodeViewModel.CreateFolder("zebra");
        zFolder.AddChild(EditorNodeViewModel.CreateTextFile("alpha.txt"));
        zFolder.AddChild(EditorNodeViewModel.CreateFolder("Zulu"));
        zFolder.AddChild(EditorNodeViewModel.CreateFolder("alpha"));
        template.AddRoot(EditorNodeViewModel.CreateTextFile("alpha.txt"));
        template.AddRoot(zFolder);
        template.AddRoot(EditorNodeViewModel.CreateFolder("Alpha"));
        template.AddRoot(EditorNodeViewModel.CreateTextFile("Beta.txt"));

        viewModel.NormalizeStructureOrder();

        Assert.Equal(["Alpha", "zebra", "alpha.txt", "Beta.txt"], template.Nodes.Select(node => node.Name));
        Assert.Equal(["alpha", "Zulu", "alpha.txt"], zFolder.Children.Select(node => node.Name));
    }

    [Fact]
    public void CreationAutomaticallyKeepsFoldersBeforeFilesAlphabetically()
    {
        MainViewModel viewModel = CreateViewModel();
        viewModel.NewTemplateCommand.Execute(null);
        viewModel.AddTextFileCommand.Execute(null);
        viewModel.AddFolderCommand.Execute(null);
        viewModel.AddFolderCommand.Execute(null);

        Assert.Equal(["New Folder", "New Folder 2", "New File"], viewModel.SelectedTemplate!.Nodes.Select(node => node.Name));
    }

    [Fact]
    public void FolderTreeCanMoveIntoFolderThatAlreadyHasChildren()
    {
        MainViewModel viewModel = CreateViewModel();
        viewModel.NewTemplateCommand.Execute(null);
        TemplateEditorViewModel template = viewModel.SelectedTemplate!;
        EditorNodeViewModel destination = EditorNodeViewModel.CreateFolder("Destination");
        destination.AddChild(EditorNodeViewModel.CreateFolder("Existing"));
        EditorNodeViewModel dragged = EditorNodeViewModel.CreateFolder("Dragged tree");
        dragged.AddChild(EditorNodeViewModel.CreateFolder("Nested"));
        template.AddRoot(destination);
        template.AddRoot(dragged);

        Assert.True(viewModel.MoveNode(dragged, destination, destination.Children.Count));
        Assert.Equal(["Dragged tree", "Existing"], destination.Children.Select(node => node.Name));
        Assert.Equal("Nested", Assert.Single(dragged.Children).Name);
    }

    [Fact]
    public async Task AttachAndReplaceFileFlowsThroughEditorAndPackageSave()
    {
        string root = Path.Combine(Path.GetTempPath(), "ProjectScaffolder.EditorTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            EditorPaths paths = new(root);
            TemplateLibraryStore store = new(paths);
            await store.LoadAsync();
            MainViewModel viewModel = new(store);
            viewModel.NewTemplateCommand.Execute(null);
            byte[] original = [1, 3, 5, 7];
            string originalPath = Path.Combine(root, "starter.max");
            await File.WriteAllBytesAsync(originalPath, original);

            await viewModel.AttachFileAsync(originalPath);

            EditorNodeViewModel asset = Assert.Single(viewModel.SelectedTemplate!.Nodes);
            Assert.True(asset.IsAssetFile);
            Assert.Equal("starter.max", asset.Name);
            Assert.Null(viewModel.SelectedNode);
            Assert.True(await viewModel.SaveCurrentTemplateAsync());
            string firstPackagePath = Path.Combine(
                paths.TemplatesRoot,
                viewModel.SelectedTemplate.TemplateId.ToString("D"),
                asset.AssetPath!.Replace('/', Path.DirectorySeparatorChar));
            Assert.Equal(original, await File.ReadAllBytesAsync(firstPackagePath));

            byte[] replacement = [2, 4, 6, 8, 10];
            string replacementPath = Path.Combine(root, "replacement.max");
            await File.WriteAllBytesAsync(replacementPath, replacement);
            viewModel.SelectedNode = asset;
            await viewModel.ReplaceSelectedAssetAsync(replacementPath);
            Assert.Equal("starter.max", asset.Name);
            Assert.True(await viewModel.SaveCurrentTemplateAsync());
            string replacementPackagePath = Path.Combine(
                paths.TemplatesRoot,
                viewModel.SelectedTemplate.TemplateId.ToString("D"),
                asset.AssetPath!.Replace('/', Path.DirectorySeparatorChar));
            Assert.Equal(replacement, await File.ReadAllBytesAsync(replacementPackagePath));
            Assert.False(File.Exists(firstPackagePath));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static MainViewModel CreateViewModel() =>
        new(new TemplateLibraryStore(new EditorPaths(Path.GetTempPath())));
}
