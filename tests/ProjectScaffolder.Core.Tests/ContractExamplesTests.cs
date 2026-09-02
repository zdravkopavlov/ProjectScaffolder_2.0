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
    public void ApprovedProjectStructureExampleIsValid()
    {
        ProjectStructureDocument structure = ValidatedContractReader.ReadProjectStructure(ReadExample("projectStructure.json"));

        Assert.Equal("DATASET/raw", structure.Paths[ProjectPathRoles.CaptureRaw]);
        Assert.Equal("DATASET/processed", structure.Paths[ProjectPathRoles.CaptureProcessed]);
        string canonicalJson = ContractJson.Serialize(structure);
        Assert.Equal(canonicalJson, ContractJson.Serialize(ValidatedContractReader.ReadProjectStructure(canonicalJson)));
    }

    [Fact]
    public void TemplateVersionTwoDeclaresCaptureRoles()
    {
        TemplateDocument template = ValidatedContractReader.ReadTemplate(ReadExample("photogrammetry-template-v2.json"));

        FolderNode dataset = Assert.IsType<FolderNode>(Assert.Single(template.Nodes));
        Assert.Equal(2, dataset.Children.Count);
        FolderNode raw = Assert.IsType<FolderNode>(dataset.Children[0]);
        FolderNode processed = Assert.IsType<FolderNode>(dataset.Children[1]);
        Assert.Equal(ProjectPathRoles.CaptureRaw, raw.Role);
        Assert.Equal(ProjectPathRoles.CaptureProcessed, processed.Role);
    }

    [Fact]
    public void DemoTemplateIsValidAndDemonstratesHiddenFolder()
    {
        TemplateDocument template = ValidatedContractReader.ReadTemplate(ReadExample("demo-template.json"));

        Assert.Equal("Demo Project", template.Name);
        Assert.Contains(template.Nodes.OfType<FolderNode>(), folder => folder.Hidden);
        Assert.Contains(template.Nodes.OfType<TextFileNode>(), file => file.Name == "README.txt");
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
