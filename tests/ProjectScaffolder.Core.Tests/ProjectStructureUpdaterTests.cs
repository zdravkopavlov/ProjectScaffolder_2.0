using ProjectScaffolder.Core.Contracts;
using ProjectScaffolder.Core.Generation;
using ProjectScaffolder.Core.Serialization;

namespace ProjectScaffolder.Core.Tests;

public sealed class ProjectStructureUpdaterTests
{
    [Fact]
    public async Task CreatesOnlyStructureMetadataForMatchingExistingProject()
    {
        string root = CreateProjectRoot("Renamed Project");
        try
        {
            TemplateDocument template = CreateTemplate();
            ProjectIdentityDocument identity = CreateIdentity(template, "Original Project");
            await WriteIdentityAsync(root, identity);
            Directory.CreateDirectory(Path.Combine(root, "DATASET", "Original Project"));
            Directory.CreateDirectory(Path.Combine(root, "DATASET", "processed"));
            string sentinelPath = Path.Combine(root, "artist-data.bin");
            await File.WriteAllTextAsync(sentinelPath, "unchanged");

            ProjectStructureUpdateResult result = await ProjectStructureUpdater.UpdateAsync(new ProjectStructureUpdateRequest
            {
                Template = template,
                TargetDirectory = root,
                UserName = "Migration User",
            });

            Assert.True(result.Created);
            Assert.Equal(identity.ProjectId, result.ProjectId);
            Assert.Equal("DATASET/Original Project", result.Paths[ProjectPathRoles.CaptureRaw]);
            Assert.Equal("DATASET/processed", result.Paths[ProjectPathRoles.CaptureProcessed]);
            Assert.Equal("unchanged", await File.ReadAllTextAsync(sentinelPath));
            ProjectStructureDocument structure = ValidatedContractReader.ReadProjectStructure(
                await File.ReadAllTextAsync(Path.Combine(root, ProjectGenerator.ProjectStructureFileName)));
            Assert.Equal(result.Paths, structure.Paths);
            Assert.False(File.Exists(Path.Combine(root, ProjectGenerator.ProjectStructureFileName + ".bak")));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task RefusesMissingDeclaredDirectoryWithoutWritingStructure()
    {
        string root = CreateProjectRoot("Missing Role Folder");
        try
        {
            TemplateDocument template = CreateTemplate();
            await WriteIdentityAsync(root, CreateIdentity(template, "Original Project"));
            Directory.CreateDirectory(Path.Combine(root, "DATASET", "Original Project"));

            ProjectStructureUpdateException exception = await Assert.ThrowsAsync<ProjectStructureUpdateException>(() =>
                ProjectStructureUpdater.UpdateAsync(new ProjectStructureUpdateRequest
                {
                    Template = template,
                    TargetDirectory = root,
                }));

            Assert.Contains(exception.Issues, issue => issue.Code == "role.directoryMissing");
            Assert.False(File.Exists(Path.Combine(root, ProjectGenerator.ProjectStructureFileName)));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task RefusesTemplateThatDoesNotMatchProjectIdentity()
    {
        string root = CreateProjectRoot("Template Mismatch");
        try
        {
            TemplateDocument recordedTemplate = CreateTemplate();
            await WriteIdentityAsync(root, CreateIdentity(recordedTemplate, "Original Project"));
            TemplateDocument selectedTemplate = CreateTemplate() with { TemplateId = Guid.CreateVersion7() };

            ProjectStructureUpdateException exception = await Assert.ThrowsAsync<ProjectStructureUpdateException>(() =>
                ProjectStructureUpdater.UpdateAsync(new ProjectStructureUpdateRequest
                {
                    Template = selectedTemplate,
                    TargetDirectory = root,
                }));

            Assert.Contains(exception.Issues, issue => issue.Code == "template.mismatch");
            Assert.False(File.Exists(Path.Combine(root, ProjectGenerator.ProjectStructureFileName)));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task RefusesInvalidExistingStructureWithoutReplacingIt()
    {
        string root = CreateProjectRoot("Invalid Structure");
        try
        {
            TemplateDocument template = CreateTemplate();
            await WriteIdentityAsync(root, CreateIdentity(template, "Original Project"));
            Directory.CreateDirectory(Path.Combine(root, "DATASET", "Original Project"));
            Directory.CreateDirectory(Path.Combine(root, "DATASET", "processed"));
            string structurePath = Path.Combine(root, ProjectGenerator.ProjectStructureFileName);
            const string invalidJson = "{ invalid structure";
            await File.WriteAllTextAsync(structurePath, invalidJson);

            await Assert.ThrowsAsync<ProjectStructureUpdateException>(() =>
                ProjectStructureUpdater.UpdateAsync(new ProjectStructureUpdateRequest
                {
                    Template = template,
                    TargetDirectory = root,
                }));

            Assert.Equal(invalidJson, await File.ReadAllTextAsync(structurePath));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task AddsCliBuilderRolesToExistingMetadataWithoutChangingProjectId()
    {
        string root = CreateProjectRoot("Existing CLIBuilder Project");
        try
        {
            TemplateDocument template = CreateCliBuilderTemplate();
            ProjectIdentityDocument identity = CreateIdentity(template, "Original Project");
            await WriteIdentityAsync(root, identity);
            foreach (string relativePath in new[]
            {
                Path.Combine("DATASET", "Original Project"),
                Path.Combine("DATASET", "processed"),
                Path.Combine("REALITYSCAN", "projects"),
                Path.Combine("REALITYSCAN", "alignment-components"),
                Path.Combine("EXPORT", "models"),
            })
            {
                Directory.CreateDirectory(Path.Combine(root, relativePath));
            }

            string structurePath = Path.Combine(root, ProjectGenerator.ProjectStructureFileName);
            await File.WriteAllTextAsync(structurePath, ContractJson.Serialize(new ProjectStructureDocument
            {
                SchemaVersion = 1,
                ProjectId = identity.ProjectId,
                Paths = new Dictionary<string, string>
                {
                    [ProjectPathRoles.CaptureRaw] = "DATASET/Original Project",
                    [ProjectPathRoles.CaptureProcessed] = "DATASET/processed",
                },
            }));

            ProjectStructureUpdateResult result = await ProjectStructureUpdater.UpdateAsync(new ProjectStructureUpdateRequest
            {
                Template = template,
                TargetDirectory = root,
            });

            Assert.False(result.Created);
            Assert.Equal(identity.ProjectId, result.ProjectId);
            Assert.Equal("REALITYSCAN/projects", result.Paths[ProjectPathRoles.RealityScanProjects]);
            Assert.Equal("REALITYSCAN/alignment-components", result.Paths[ProjectPathRoles.RealityScanAlignmentComponents]);
            Assert.Equal("EXPORT/models", result.Paths[ProjectPathRoles.ModelExports]);
            ProjectStructureDocument persisted = ValidatedContractReader.ReadProjectStructure(
                await File.ReadAllTextAsync(structurePath));
            Assert.Equal(identity.ProjectId, persisted.ProjectId);
            Assert.Equal(5, persisted.Paths.Count);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static TemplateDocument CreateTemplate()
    {
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        return new TemplateDocument
        {
            SchemaVersion = 2,
            TemplateId = Guid.CreateVersion7(timestamp),
            Name = "Photogrammetry",
            Description = null,
            TemplateVersion = 7,
            CreatedAtUtc = timestamp,
            ModifiedAtUtc = timestamp,
            DefaultFileCollision = FileCollisionPolicy.Skip,
            Nodes =
            [
                new FolderNode
                {
                    NodeId = Guid.CreateVersion7(timestamp.AddMilliseconds(1)),
                    Name = "DATASET",
                    Children =
                    [
                        new FolderNode
                        {
                            NodeId = Guid.CreateVersion7(timestamp.AddMilliseconds(2)),
                            Name = "{{project_name}}",
                            Role = ProjectPathRoles.CaptureRaw,
                            Children = [],
                        },
                        new FolderNode
                        {
                            NodeId = Guid.CreateVersion7(timestamp.AddMilliseconds(3)),
                            Name = "processed",
                            Role = ProjectPathRoles.CaptureProcessed,
                            Children = [],
                        },
                    ],
                },
            ],
        };
    }

    private static TemplateDocument CreateCliBuilderTemplate()
    {
        TemplateDocument template = CreateTemplate();
        return template with
        {
            Nodes =
            [
                .. template.Nodes,
                new FolderNode
                {
                    NodeId = Guid.CreateVersion7(),
                    Name = "REALITYSCAN",
                    Children =
                    [
                        new FolderNode
                        {
                            NodeId = Guid.CreateVersion7(),
                            Name = "projects",
                            Role = ProjectPathRoles.RealityScanProjects,
                            Children = [],
                        },
                        new FolderNode
                        {
                            NodeId = Guid.CreateVersion7(),
                            Name = "alignment-components",
                            Role = ProjectPathRoles.RealityScanAlignmentComponents,
                            Children = [],
                        },
                    ],
                },
                new FolderNode
                {
                    NodeId = Guid.CreateVersion7(),
                    Name = "EXPORT",
                    Children =
                    [
                        new FolderNode
                        {
                            NodeId = Guid.CreateVersion7(),
                            Name = "models",
                            Role = ProjectPathRoles.ModelExports,
                            Children = [],
                        },
                    ],
                },
            ],
        };
    }

    private static ProjectIdentityDocument CreateIdentity(TemplateDocument template, string projectName) => new()
    {
        SchemaVersion = 1,
        ProjectId = Guid.CreateVersion7(),
        CreatedAtUtc = DateTimeOffset.UtcNow,
        ProjectName = projectName,
        Template = new ProjectTemplateProvenance
        {
            Id = template.TemplateId,
            Name = template.Name,
            Version = template.TemplateVersion,
        },
        Generator = new GeneratorProvenance
        {
            Name = "Project Scaffolder",
            Version = "2.0.0-alpha.4",
        },
    };

    private static async Task WriteIdentityAsync(string root, ProjectIdentityDocument identity) =>
        await File.WriteAllTextAsync(
            Path.Combine(root, ProjectGenerator.ProjectIdentityFileName),
            ContractJson.Serialize(identity));

    private static string CreateProjectRoot(string name)
    {
        string root = Path.Combine(Path.GetTempPath(), "ProjectScaffolder.StructureUpdateTests", Guid.NewGuid().ToString("N"), name);
        Directory.CreateDirectory(root);
        return root;
    }
}
