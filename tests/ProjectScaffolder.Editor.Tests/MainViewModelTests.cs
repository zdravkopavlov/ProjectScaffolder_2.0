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

    private static MainViewModel CreateViewModel() =>
        new(new TemplateLibraryStore(new EditorPaths(Path.GetTempPath())));
}
