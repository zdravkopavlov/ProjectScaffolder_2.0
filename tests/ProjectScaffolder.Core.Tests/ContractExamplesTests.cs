using ProjectScaffolder.Core.Contracts;
using ProjectScaffolder.Core.Serialization;

namespace ProjectScaffolder.Core.Tests;

public sealed class ContractExamplesTests
{
    [Fact]
    public void ApprovedTemplateExampleIsValid()
    {
        string json = ReadExample("3ds-max-template.json");

        TemplateDocument template = ValidatedContractReader.ReadTemplate(json);

        Assert.Equal("3ds Max Project", template.Name);
        Assert.Equal(4, template.Nodes.Count);
        Assert.IsType<TextFileNode>(template.Nodes[^1]);
        string canonicalJson = ContractJson.Serialize(template);
        Assert.Equal(canonicalJson, ContractJson.Serialize(ValidatedContractReader.ReadTemplate(canonicalJson)));
    }

    [Fact]
    public void ApprovedProjectIdentityExampleIsValid()
    {
        string json = ReadExample("projectID.json");

        ProjectIdentityDocument identity = ValidatedContractReader.ReadProjectIdentity(json);

        Assert.Equal("Hotel Lobby", identity.ProjectName);
        Assert.Equal("Project Scaffolder", identity.Generator.Name);
        Assert.Equal(identity, ValidatedContractReader.ReadProjectIdentity(ContractJson.Serialize(identity)));
    }

    [Fact]
    public void ApprovedTemplateLibraryExampleIsValid()
    {
        string json = ReadExample("template-library.json");

        TemplateLibraryDocument library = ValidatedContractReader.ReadTemplateLibrary(json);

        Assert.Single(library.TemplateOrder);
        Assert.Single(library.ExplorerTemplateIds);
        string canonicalJson = ContractJson.Serialize(library);
        Assert.Equal(canonicalJson, ContractJson.Serialize(ValidatedContractReader.ReadTemplateLibrary(canonicalJson)));
    }

    [Fact]
    public void UnknownPropertiesAreRejected()
    {
        string json = ReadExample("template-library.json").Replace(
            "\"schemaVersion\": 1,",
            "\"schemaVersion\": 1, \"unexpected\": true,",
            StringComparison.Ordinal);

        Assert.Throws<System.Text.Json.JsonException>(() => ContractJson.DeserializeTemplateLibrary(json));
    }

    [Fact]
    public void EnumValuesAreCaseSensitive()
    {
        string json = ReadExample("3ds-max-template.json").Replace(
            "\"defaultFileCollision\": \"skip\"",
            "\"defaultFileCollision\": \"Skip\"",
            StringComparison.Ordinal);

        Assert.Throws<System.Text.Json.JsonException>(() => ContractJson.DeserializeTemplate(json));
    }

    private static string ReadExample(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "examples", name));
}
