using ProjectScaffolder.Core.Validation;

namespace ProjectScaffolder.Core.Tests;

public sealed class WindowsNameValidatorTests
{
    [Theory]
    [InlineData("SCENES")]
    [InlineData("{{project_name}}.mxp")]
    [InlineData("README.md")]
    public void AcceptsSafeNames(string name)
    {
        Assert.Null(WindowsNameValidator.GetError(name));
    }

    [Theory]
    [InlineData("CON")]
    [InlineData("con.txt")]
    [InlineData("LPT9.log")]
    [InlineData("bad/name")]
    [InlineData("trailing.")]
    [InlineData("..")]
    public void RejectsUnsafeNames(string name)
    {
        Assert.NotNull(WindowsNameValidator.GetError(name));
    }
}
