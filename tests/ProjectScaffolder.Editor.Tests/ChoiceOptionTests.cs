using System.IO;
using ProjectScaffolder.Editor.Services;
using ProjectScaffolder.Editor.ViewModels;

namespace ProjectScaffolder.Editor.Tests;

public sealed class ChoiceOptionTests
{
    [Fact]
    public void CollisionChoicesBeginWithExplicitTemplateDefault()
    {
        MainViewModel viewModel = new(new TemplateLibraryStore(new EditorPaths(Path.GetTempPath())));

        ChoiceOption<ProjectScaffolder.Core.Contracts.FileCollisionPolicy?> first = viewModel.FileCollisionChoices[0];

        Assert.Null(first.Value);
        Assert.Equal("Use template default", first.Label);
    }
}
