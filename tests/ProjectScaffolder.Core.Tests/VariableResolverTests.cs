using ProjectScaffolder.Core.Variables;

namespace ProjectScaffolder.Core.Tests;

public sealed class VariableResolverTests
{
    [Fact]
    public void ResolvesBuiltInVariablesInOnePass()
    {
        Dictionary<string, string> values = new(StringComparer.Ordinal)
        {
            ["project_name"] = "Hotel Lobby",
            ["project_path"] = "D:\\Projects\\Hotel Lobby",
            ["project_id"] = "{{user_name}}",
            ["created_at_utc"] = "2026-08-24T12:00:00.000Z",
            ["created_date"] = "2026-08-24",
            ["user_name"] = "Zdravko",
        };

        string result = VariableResolver.Resolve("{{project_name}}|{{project_id}}", values);

        Assert.Equal("Hotel Lobby|{{user_name}}", result);
    }

    [Theory]
    [InlineData("{{Project_Name}}")]
    [InlineData("{{unknown}}")]
    [InlineData("{{project_name}")]
    [InlineData("project_name}}")]
    public void RejectsUnknownOrMalformedVariables(string value)
    {
        Dictionary<string, string> values = VariableResolver.BuiltInNames.ToDictionary(name => name, name => name);

        Assert.Throws<VariableResolutionException>(() => VariableResolver.Resolve(value, values));
    }
}
