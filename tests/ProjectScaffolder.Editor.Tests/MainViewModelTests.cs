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
        EditorNodeViewModel folder = viewModel.SelectedNode!;
        viewModel.AddTextFileCommand.Execute(null);
        EditorNodeViewModel firstFile = viewModel.SelectedNode!;

        viewModel.SelectedNode = firstFile;
        viewModel.AddTextFileCommand.Execute(null);

        Assert.Equal(2, folder.Children.Count);
        Assert.All(folder.Children, node => Assert.True(node.IsTextFile));
    }

    [Fact]
    public void MoveNodeSupportsReorderingNestingAndReturningToRoot()
    {
        MainViewModel viewModel = CreateViewModel();
        viewModel.NewTemplateCommand.Execute(null);
        viewModel.AddFolderCommand.Execute(null);
        EditorNodeViewModel first = viewModel.SelectedNode!;
        viewModel.SelectedNode = null;
        viewModel.AddFolderCommand.Execute(null);
        EditorNodeViewModel second = viewModel.SelectedNode!;

        Assert.True(viewModel.MoveNode(second, null, 0));
        Assert.Same(second, viewModel.SelectedTemplate!.Nodes[0]);
        Assert.True(viewModel.MoveNode(first, second, 0));
        Assert.Same(first, Assert.Single(second.Children));
        Assert.True(viewModel.MoveNode(first, null, viewModel.SelectedTemplate.Nodes.Count));
        Assert.Same(first, viewModel.SelectedTemplate.Nodes[^1]);
    }

    [Fact]
    public void MoveNodeRejectsDroppingFolderInsideItsDescendant()
    {
        MainViewModel viewModel = CreateViewModel();
        viewModel.NewTemplateCommand.Execute(null);
        viewModel.AddFolderCommand.Execute(null);
        EditorNodeViewModel parent = viewModel.SelectedNode!;
        viewModel.AddFolderCommand.Execute(null);
        EditorNodeViewModel child = viewModel.SelectedNode!;

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

        viewModel.RenameNodeCommand.Execute(null);

        Assert.True(viewModel.SelectedNode!.IsEditing);
    }

    private static MainViewModel CreateViewModel() =>
        new(new TemplateLibraryStore(new EditorPaths(Path.GetTempPath())));
}
