using ProjectScaffolder.Core.Contracts;
using ProjectScaffolder.Core.Validation;

namespace ProjectScaffolder.Core.Tests;

public sealed class ProjectStructureContractTests
{
    [Fact]
    public void TemplateVersionOneRejectsSemanticRole()
    {
        TemplateDocument template = CreateTemplate(1,
        [
            CreateFolder("raw", ProjectPathRoles.CaptureRaw),
        ]);

        ContractValidationResult result = TemplateValidator.Validate(template);

        Assert.Contains(result.Issues, issue => issue.Code == "role.schema");
    }

    [Fact]
    public void TemplateRejectsDuplicateSemanticRoles()
    {
        TemplateDocument template = CreateTemplate(2,
        [
            CreateFolder("raw-a", ProjectPathRoles.CaptureRaw),
            CreateFolder("raw-b", ProjectPathRoles.CaptureRaw),
        ]);

        ContractValidationResult result = TemplateValidator.Validate(template);

        Assert.Contains(result.Issues, issue => issue.Code == "role.duplicate");
    }

    [Theory]
    [InlineData("../raw")]
    [InlineData("DATASET\\raw")]
    [InlineData("/DATASET/raw")]
    [InlineData("DATASET//raw")]
    public void ProjectStructureRejectsUnsafeRelativePath(string path)
    {
        var structure = new ProjectStructureDocument
        {
            SchemaVersion = 1,
            ProjectId = Guid.CreateVersion7(),
            Paths = new Dictionary<string, string> { [ProjectPathRoles.CaptureRaw] = path },
        };

        ContractValidationResult result = ProjectStructureValidator.Validate(structure);

        Assert.Contains(result.Issues, issue => issue.Code == "path.invalid");
    }

    [Fact]
    public void CliBuilderRolesFitTemplateVersionTwoAndProjectStructureVersionOne()
    {
        string[] roles =
        [
            ProjectPathRoles.RealityScanProjects,
            ProjectPathRoles.RealityScanAlignmentComponents,
            ProjectPathRoles.ModelExports,
        ];
        TemplateDocument template = CreateTemplate(
            2,
            roles.Select((role, index) => CreateFolder($"destination-{index}", role)).ToArray());
        var structure = new ProjectStructureDocument
        {
            SchemaVersion = 1,
            ProjectId = Guid.CreateVersion7(),
            Paths = roles.ToDictionary(role => role, role => $"DESTINATIONS/{role.Replace('.', '-')}"),
        };

        Assert.True(TemplateValidator.Validate(template).IsValid);
        Assert.True(ProjectStructureValidator.Validate(structure).IsValid);
    }

    private static FolderNode CreateFolder(string name, string? role = null) => new()
    {
        NodeId = Guid.CreateVersion7(),
        Name = name,
        Role = role,
        Children = [],
    };

    private static TemplateDocument CreateTemplate(int schemaVersion, IReadOnlyList<TemplateNode> nodes)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        return new TemplateDocument
        {
            SchemaVersion = schemaVersion,
            TemplateId = Guid.CreateVersion7(),
            Name = "Structure Test",
            TemplateVersion = 1,
            CreatedAtUtc = now,
            ModifiedAtUtc = now,
            DefaultFileCollision = FileCollisionPolicy.Skip,
            Nodes = nodes,
        };
    }
}
