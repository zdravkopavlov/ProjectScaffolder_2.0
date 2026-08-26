using ProjectScaffolder.Editor.Services;

namespace ProjectScaffolder.Editor.Tests;

public sealed class ExplorerMenuPlanBuilderTests
{
    [Fact]
    public void BuildsBackgroundAndSelectedFolderCommandsWithCorrectTargets()
    {
        Guid firstId = Guid.Parse("01993a6c-f686-7e81-b530-35940e1b4072");
        Guid secondId = Guid.Parse("01993a6d-1111-7111-8111-111111111111");

        ExplorerMenuPlan plan = ExplorerMenuPlanBuilder.Build(
            @"C:\Program Files\Project Scaffolder\ProjectScaffolder.Editor.exe",
            @"C:\Program Files\Project Scaffolder\ProjectScaffolder.Runner.exe",
            [new(firstId, "3ds Max Project"), new(secondId, "Photogrammetry")]);

        Assert.Equal("New Project", plan.MenuLabel);
        Assert.Equal(2, plan.Locations.Count);
        ExplorerMenuLocation background = Assert.Single(plan.Locations, location => location.RegistryPath.Contains("Background", StringComparison.Ordinal));
        ExplorerMenuLocation selected = Assert.Single(plan.Locations, location => !location.RegistryPath.Contains("Background", StringComparison.Ordinal));
        Assert.Contains("--target \"%V\"", background.Commands[0].CommandLine, StringComparison.Ordinal);
        Assert.Contains("--target \"%1\"", selected.Commands[0].CommandLine, StringComparison.Ordinal);
        Assert.Contains(firstId.ToString("D"), background.Commands[0].CommandLine, StringComparison.Ordinal);
        Assert.Equal(["3ds Max Project", "Photogrammetry", "Editor"], background.Commands.Select(command => command.Label));
        Assert.True(background.Commands[^1].SeparatorBefore);
        Assert.All(background.Commands.Take(background.Commands.Count - 1), command => Assert.False(command.SeparatorBefore));
    }

    [Fact]
    public void QuotesExecutablePathsAndKeepsEditorAsOnlyCommandWhenNoTemplatesAreEnabled()
    {
        ExplorerMenuPlan plan = ExplorerMenuPlanBuilder.Build(@"C:\Apps With Spaces\Editor.exe", @"C:\Apps With Spaces\Runner.exe", []);

        ExplorerMenuCommand command = Assert.Single(plan.Locations[0].Commands);
        Assert.Equal("Editor", command.Label);
        Assert.Equal("\"C:\\Apps With Spaces\\Editor.exe\"", command.CommandLine);
        Assert.True(command.SeparatorBefore);
    }
}
