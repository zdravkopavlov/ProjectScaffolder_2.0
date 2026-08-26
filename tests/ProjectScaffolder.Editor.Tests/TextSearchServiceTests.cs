using ProjectScaffolder.Editor.Services;

namespace ProjectScaffolder.Editor.Tests;

public sealed class TextSearchServiceTests
{
    [Fact]
    public void FindNextIgnoresCaseAndWraps()
    {
        const string text = "Alpha beta ALPHA";

        Assert.Equal(0, TextSearchService.FindNext(text, "alpha", 12));
        Assert.Equal(11, TextSearchService.FindNext(text, "alpha", 1));
    }

    [Fact]
    public void ReplaceAllTreatsSearchAndReplacementAsLiteralText()
    {
        (string text, int count) = TextSearchService.ReplaceAll("A.b a.B", ".b", "$1");

        Assert.Equal("A$1 a$1", text);
        Assert.Equal(2, count);
    }

    [Fact]
    public void EmptySearchDoesNotChangeContent()
    {
        (string text, int count) = TextSearchService.ReplaceAll("original", string.Empty, "replacement");

        Assert.Equal("original", text);
        Assert.Equal(0, count);
    }
}
