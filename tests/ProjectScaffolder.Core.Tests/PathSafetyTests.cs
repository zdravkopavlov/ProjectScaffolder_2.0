using ProjectScaffolder.Core.Paths;

namespace ProjectScaffolder.Core.Tests;

public sealed class PathSafetyTests
{
    [Fact]
    public void ResolvesOrdinaryDescendant()
    {
        string root = Path.Combine(Path.GetTempPath(), "ProjectScaffolder.PathTests", "Root");

        string result = PathSafety.ResolveDescendant(root, ["ASSETS", "Textures"]);

        Assert.Equal(Path.GetFullPath(Path.Combine(root, "ASSETS", "Textures")), result);
    }

    [Theory]
    [InlineData("..")]
    [InlineData(".")]
    [InlineData("folder\\child")]
    public void RejectsAnythingOtherThanOneSafeComponent(string component)
    {
        string root = Path.Combine(Path.GetTempPath(), "ProjectScaffolder.PathTests", "Root");

        Assert.Throws<ArgumentException>(() => PathSafety.ResolveDescendant(root, [component]));
    }

    [Fact]
    public void PrefixSiblingIsNotTreatedAsDescendant()
    {
        string parent = Path.Combine(Path.GetTempPath(), "ProjectScaffolder.PathTests");
        string root = Path.Combine(parent, "Project");
        string sibling = Path.Combine(parent, "Project Copy", "file.txt");

        Assert.False(PathSafety.IsStrictDescendant(root, sibling));
    }
}
