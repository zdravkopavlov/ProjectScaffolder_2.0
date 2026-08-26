using System.IO;
using ProjectScaffolder.Editor.Services;

namespace ProjectScaffolder.Editor.Tests;

public sealed class EditorStartupOptionsTests
{
    [Fact]
    public void ParsesDataRootAndSelectionSmokeOptionInEitherOrder()
    {
        string path = Path.Combine(Path.GetTempPath(), "ProjectScaffolder Startup Test");

        EditorStartupOptions options = EditorStartupOptions.Parse(["--smoke-select-first", "--data-root", path]);

        Assert.True(options.SmokeSelectFirstNode);
        Assert.False(options.EnableExplorerRegistration);
        Assert.Equal(Path.GetFullPath(path), options.Paths.DataRoot);
    }

    [Fact]
    public void RejectsUnknownOrDuplicateOptions()
    {
        Assert.Throws<ArgumentException>(() => EditorStartupOptions.Parse(["--unknown"]));
        Assert.Throws<ArgumentException>(() => EditorStartupOptions.Parse(["--smoke-select-first", "--smoke-select-first"]));
    }
}
